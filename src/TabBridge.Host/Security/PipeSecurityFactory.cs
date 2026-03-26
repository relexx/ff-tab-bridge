using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace TabBridge.Host.Security;

/// <summary>Creates a <see cref="PipeSecurity"/> that denies Everyone and allows only the current user.</summary>
public static class PipeSecurityFactory
{
    /// <summary>
    /// Returns a <see cref="PipeSecurity"/> with:
    /// <list type="bullet">
    ///   <item>Deny FullControl to Everyone (WellKnownSidType.WorldSid)</item>
    ///   <item>Allow ReadWrite to the current user SID</item>
    /// </list>
    /// This satisfies security rules #9 and #10.
    /// </summary>
    public static PipeSecurity CreateRestrictedPipeSecurity()
    {
        PipeSecurity security = new();
        SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User!;

        // Security rule #9: Deny Everyone
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Deny));

        // Security rule #9: Allow only current user
        security.AddAccessRule(new PipeAccessRule(
            currentUser,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return security;
    }

    /// <summary>Returns the Named Pipe name scoped to the current user's SID.</summary>
    public static string GetPipeName()
    {
        string userSid = WindowsIdentity.GetCurrent().User!.Value;
        return $"tab-bridge-{userSid}";
    }
}
