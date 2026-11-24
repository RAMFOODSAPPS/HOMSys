using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// Data only (user request 2026-11-24): the branch BAs from Sheet1 of `RAM BRANCHES ACTIVECUSTOMER LIST 2.xlsx` as
    /// BRANCH ADMINISTRATOR users. Username = Employee ID, the branch's Site / Branch Code, and that branch as their servicing
    /// branch. No password is kept in the code: each new user gets the hash of a random value that is thrown away, so nobody
    /// can sign in until an Admin clicks Reset Password on the Users page (which shows a temporary password; must change at
    /// first login). A BA whose username OR email already exists is skipped (never overwritten). Also gives BRANCH
    /// ADMINISTRATOR the `tpar` permission (New TPAR / TPAR List).
    /// </summary>
    public partial class SeedBranchBaUsers : Migration
    {
        private const string Marker = "SeedBranchBaUsers";

        // Username (Employee ID), email, first name, last name, branch Site code.
        private static readonly (string User, string Email, string First, string Last, string Site)[] Bas =
        [
            ("03005185", "Isabela-BA@ramfoods.com",   "Reila",         "Abenojar",   "ISA-B"),
            ("03004956", "dagupan-ba@ramfoods.com",   "Divina",        "Carrera",    "DAG-B"),
            ("03005886", "legazpi-ba@ramfoods.com",   "Ronielyn",      "Boneo",      "LEG-B"),
            ("03004210", "naga-ba@ramfoods.com",      "Merelynn",      "Delica",     "NAG-B"),
            ("03005659", "roxas-ba@ramfoods.com",     "Jenamie",       "Juarez",     "ROX-B"),
            ("03005710", "bacolod-ba@ramfoods.com",   "Marcia",        "Velarde",    "BAC-B"),
            ("03005434", "dumaguete-ba@ramfoods.com", "Clarissa",      "Pestañas", "DUM-B"),
            ("03005927", "cebu-ba@ramfoods.com",      "Estoya",        "Bernabe",    "CEB-B"),
            ("03005182", "iloilo-ba@ramfoods.com",    "Gethrie Mae",   "Palmes",     "ILO-B"),
            ("03005759", "tacloban-ba@ramfoods.com",  "Maria Ruth",    "Pacantara",  "TAC-B"),
            ("03003199", "vderiada@ramfoods.com",     "Venus",         "Alberca",    "DAV-B"),
            ("03004517", "kidapawan-ba@ramfoods.com", "Honey",         "Baure",      "KID-B"),
            ("03004425", "gensan-ba@ramfoods.com",    "Anna Rhea",     "Conje",      "GEN-B"),
            ("03005514", "panabo-ba@ramfoods.com",    "Lourdes",       "Gumisad",    "PAN-B"),
            ("03005304", "tagum-ba@ramfoods.com",     "Devie Jean",    "Tamarion",   "TGM-B"),
            ("03005336", "cagayan-ba@ramfoods.com",   "Maricel",       "Jayme",      "CDO-B"),
            ("03005681", "butuan-ba@ramfoods.com",    "Jayle Mae",     "Oniot",      "BUT-B"),
            ("03005486", "ozamis-ba@ramfoods.com",    "Maikyla Irene", "Tapia",      "OZA-B"),
            ("03005374", "zamboanga-ba@ramfoods.com", "Dionelda",      "Padilla",    "ZAM-B"),
            ("03001785", "ecueva@ramfoods.com",       "Edna",          "Cueva",      "HON-B"),
        ];

        private static string Q(string v) => "N'" + v.Replace("'", "''") + "'";

        /// <summary>BCrypt hash of a random value nobody keeps: the account can't be signed in to until reset.</summary>
        private static string UnusableHash() =>
            BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var values = string.Join(",\n", Bas.Select(b =>
                $"({Q(b.User)}, {Q(b.Email)}, {Q(b.First)}, {Q(b.Last)}, {Q(b.Site)}, {Q(UnusableHash())})"));

            migrationBuilder.Sql($@"
DECLARE @ba TABLE (Username nvarchar(50), Email nvarchar(100), FirstName nvarchar(50), LastName nvarchar(50),
                   SiteCode nvarchar(20), PasswordHash nvarchar(200));
INSERT INTO @ba (Username, Email, FirstName, LastName, SiteCode, PasswordHash) VALUES
{values};

INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, IsActive, MustChangePassword, CreatedAt,
                   CompanyId, DepartmentId, SiteId, CreatedBy, BranchCode)
SELECT b.Username, b.Email, b.PasswordHash, b.FirstName, b.LastName, 1, 1, SYSUTCDATETIME(),
       (SELECT Id FROM Companies WHERE Id = 1), (SELECT Id FROM Departments WHERE Id = 2), s.Id, N'{Marker}', b.SiteCode
FROM @ba b
LEFT JOIN Sites s ON s.Code = b.SiteCode
WHERE NOT EXISTS (SELECT 1 FROM Users u WHERE u.Username = b.Username OR u.Email = b.Email);

INSERT INTO UserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM Users u JOIN @ba b ON b.Username = u.Username
CROSS JOIN Roles r
WHERE r.Name = N'BRANCH ADMINISTRATOR' AND u.CreatedBy = N'{Marker}'
  AND NOT EXISTS (SELECT 1 FROM UserRoles x WHERE x.UserId = u.Id AND x.RoleId = r.Id);

INSERT INTO UserServicingBranches (UserId, SiteId)
SELECT u.Id, s.Id
FROM Users u JOIN @ba b ON b.Username = u.Username
JOIN Sites s ON s.Code = b.SiteCode
WHERE u.CreatedBy = N'{Marker}'
  AND NOT EXISTS (SELECT 1 FROM UserServicingBranches x WHERE x.UserId = u.Id AND x.SiteId = s.Id);

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r CROSS JOIN Permissions p
WHERE r.Name = N'BRANCH ADMINISTRATOR' AND p.[Key] = N'tpar'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);
");
        }

        /// <inheritdoc />
        /// <remarks>Removes only the BAs this migration created that never logged in; the role's tpar permission stays.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DECLARE @ids TABLE (Id int);
INSERT INTO @ids SELECT Id FROM Users WHERE CreatedBy = N'{Marker}' AND LastLoginAt IS NULL;
DELETE FROM UserServicingBranches WHERE UserId IN (SELECT Id FROM @ids);
DELETE FROM UserRoles WHERE UserId IN (SELECT Id FROM @ids);
DELETE FROM Users WHERE Id IN (SELECT Id FROM @ids);
");
        }
    }
}
