using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// TASAF sales users and position roles (trade marketing list, 2026-10-19).
    /// Roles KAM (TASAF) and GKAM / TMM / NSM / NSD (TASAF + View All); the existing COO role
    /// also gets TASAF + View All (Imee Ferrer keeps her "iferrer" account).
    /// Username = "0300" + employee ID, email = first initial + last name @ramfoods.com,
    /// Department SALES, Site Head Office, no branch. Existing usernames / roles are skipped.
    /// The password hash matches no known password: an admin issues the first password with
    /// Users › Reset Password (must be changed at first login). TASAF approvers are set in
    /// TASAF Setup › Approval Matrix, not here.
    /// </summary>
    public partial class AddTasafUsersAndRoles : Migration
    {
        private const string By = "migration-tasaf-users";

        private const string Roles = """
            ('KAM',  'Key Account Manager - TASAF proponent'),
            ('GKAM', 'Group Key Account Manager - TASAF approver'),
            ('TMM',  'Trade Marketing Manager - TASAF approver'),
            ('NSM',  'National Sales Manager - TASAF approver'),
            ('NSD',  'National Sales Director - TASAF approver')
            """;

        private const string RolePerms = """
            ('KAM',  'tasaf'),
            ('GKAM', 'tasaf'), ('GKAM', 'tasaf-view-all'),
            ('TMM',  'tasaf'), ('TMM',  'tasaf-view-all'),
            ('NSM',  'tasaf'), ('NSM',  'tasaf-view-all'),
            ('NSD',  'tasaf'), ('NSD',  'tasaf-view-all'),
            ('COO',  'tasaf'), ('COO',  'tasaf-view-all')
            """;

        private const string Users = """
            ('03005603', 'Jerome',   'Jason',      'jjason@ramfoods.com',      'KAM'),
            ('03006003', 'Jake',     'Servania',   'jservania@ramfoods.com',   'KAM'),
            ('03005978', 'Rovie',    'Olaso',      'rolaso@ramfoods.com',      'KAM'),
            ('03005995', 'Francis',  'Pedrezuela', 'fpedrezuela@ramfoods.com', 'KAM'),
            ('03005949', 'Stefanie', 'Lupian',     'slupian@ramfoods.com',     'KAM'),
            ('03005977', 'Jason',    'Retiza',     'jretiza@ramfoods.com',     'KAM'),
            ('03005699', 'Janne',    'Makiling',   'jmakiling@ramfoods.com',   'KAM'),
            ('03005944', 'Teresa',   'Araneta',    'taraneta@ramfoods.com',    'KAM'),
            ('03005975', 'Aldrin',   'Liwasan',    'aliwasan@ramfoods.com',    'KAM'),
            ('03005991', 'Patrick',  'Munoz',      'pmunoz@ramfoods.com',      'KAM'),
            ('03005701', 'Andrew',   'Aniceto',    'aaniceto@ramfoods.com',    'KAM'),
            ('03005911', 'Marlon',   'Sunga',      'msunga@ramfoods.com',      'KAM'),
            ('03005903', 'Renan',    'Marukot',    'rmarukot@ramfoods.com',    'KAM'),
            ('03005697', 'Carl',     'Yap',        'cyap@ramfoods.com',        'KAM'),
            ('03005623', 'Roy',      'Aguhob',     'raguhob@ramfoods.com',     'KAM'),
            ('03005976', 'Mika',     'Visaya',     'mvisaya@ramfoods.com',     'GKAM'),
            ('03005812', 'Regina',   'Calpo',      'rcalpo@ramfoods.com',      'TMM'),
            ('03005715', 'Marriel',  'Arda',       'marda@ramfoods.com',       'NSM'),
            ('03005661', 'Joerevel', 'Asuncion',   'jasuncion@ramfoods.com',   'NSM')
            """;

        // bcrypt-format hash of nothing anyone knows; Reset Password issues the real one.
        private const string NoPassword = "$2a$11$EjPQtOA0dH4mOKNkb7qohaLrVQ3kKv6cJqv/s7y/2hTwwkybHzXGG";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO [Roles] ([Name], [Description], [CreatedAt], [CreatedBy])
                SELECT v.[Name], v.[Descr], GETUTCDATE(), '{By}'
                FROM (VALUES {Roles}) v([Name], [Descr])
                WHERE NOT EXISTS (SELECT 1 FROM [Roles] r WHERE r.[Name] = v.[Name]);

                INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
                SELECT r.[Id], p.[Id]
                FROM (VALUES {RolePerms}) v([Role], [Perm])
                JOIN [Roles] r ON r.[Name] = v.[Role]
                JOIN [Permissions] p ON p.[Key] = v.[Perm]
                WHERE NOT EXISTS (SELECT 1 FROM [RolePermissions] x WHERE x.[RoleId] = r.[Id] AND x.[PermissionId] = p.[Id]);

                DECLARE @company int = (SELECT TOP 1 [Id] FROM [Companies] ORDER BY [Id]);
                DECLARE @dept int    = (SELECT TOP 1 [Id] FROM [Departments] WHERE [Name] = 'SALES');
                DECLARE @site int    = (SELECT TOP 1 [Id] FROM [Sites] WHERE [Code] = 'HO');

                INSERT INTO [Users] ([Username], [Email], [PasswordHash], [FirstName], [LastName], [CompanyId], [DepartmentId],
                                     [SiteId], [BranchCode], [IsActive], [MustChangePassword], [CreatedAt], [CreatedBy])
                SELECT v.[Username], v.[Email], '{NoPassword}', v.[First], v.[Last], @company, @dept,
                       @site, NULL, 1, 1, GETUTCDATE(), '{By}'
                FROM (VALUES {Users}) v([Username], [First], [Last], [Email], [Role])
                WHERE NOT EXISTS (SELECT 1 FROM [Users] u WHERE u.[Username] = v.[Username]);

                INSERT INTO [UserRoles] ([UserId], [RoleId])
                SELECT u.[Id], r.[Id]
                FROM (VALUES {Users}) v([Username], [First], [Last], [Email], [Role])
                JOIN [Users] u ON u.[Username] = v.[Username] AND u.[CreatedBy] = '{By}'
                JOIN [Roles] r ON r.[Name] = v.[Role]
                WHERE NOT EXISTS (SELECT 1 FROM [UserRoles] x WHERE x.[UserId] = u.[Id] AND x.[RoleId] = r.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE ur FROM [UserRoles] ur JOIN [Users] u ON u.[Id] = ur.[UserId]
                WHERE u.[CreatedBy] = '{By}' AND u.[LastLoginAt] IS NULL;
                DELETE FROM [Users] WHERE [CreatedBy] = '{By}' AND [LastLoginAt] IS NULL;

                DELETE rp FROM [RolePermissions] rp
                JOIN [Roles] r ON r.[Id] = rp.[RoleId]
                JOIN [Permissions] p ON p.[Id] = rp.[PermissionId]
                WHERE r.[Name] = 'COO' AND p.[Key] IN ('tasaf', 'tasaf-view-all');

                DELETE rp FROM [RolePermissions] rp JOIN [Roles] r ON r.[Id] = rp.[RoleId]
                WHERE r.[CreatedBy] = '{By}' AND NOT EXISTS (SELECT 1 FROM [UserRoles] ur WHERE ur.[RoleId] = r.[Id]);
                DELETE r FROM [Roles] r
                WHERE r.[CreatedBy] = '{By}' AND NOT EXISTS (SELECT 1 FROM [UserRoles] ur WHERE ur.[RoleId] = r.[Id]);
                """);
        }
    }
}
