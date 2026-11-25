namespace HOMSys.Application.Interfaces;

/// <summary>
/// Creates the Draft post evaluation of a TASAF that just became Completed (pre-filled from the TASAF) and tells the
/// proponent to fill it in (user decision 2026-11-24). Implemented by Infrastructure's TasafPostEvalService.
/// </summary>
public interface ITasafPostEvalStarter
{
    /// <summary>Does nothing unless the TASAF is Completed. reason = why it completed, for the message. True = notified.</summary>
    Task<bool> StartAsync(int tasafId, string reason);
}
