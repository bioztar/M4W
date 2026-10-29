using System.Security.Cryptography;
using M4W.Core;

namespace M4W.App;

/// <summary>Encrypts item blobs with Windows DPAPI, tied to the current Windows login.</summary>
internal sealed class DpapiProtector : IProtector
{
    public byte[] Protect(byte[] plain) => ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedBytes) => ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
}
