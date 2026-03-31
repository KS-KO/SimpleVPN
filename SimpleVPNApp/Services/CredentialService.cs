using System;
using System.Runtime.InteropServices;
using System.Text;
using SimpleVPNApp.Helpers;

namespace SimpleVPNApp.Services;

/// <summary>
/// Windows Credential Manager와 상호 작용하여 비밀번호 및 토큰을 안전하게 관리합니다.
/// </summary>
public sealed class CredentialService
{
    private const string TargetPrefix = "SimpleVPN:";

    public void SaveCredential(string key, string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        var targetName = TargetPrefix + key;
        var blob = Encoding.Unicode.GetBytes(value);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        Marshal.Copy(blob, 0, blobPtr, blob.Length);

        try
        {
            var credential = new NativeMethods.CREDENTIAL
            {
                Type = NativeMethods.CRED_TYPE_GENERIC,
                TargetName = targetName,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = NativeMethods.CRED_PERSIST_LOCAL_MACHINE,
                UserName = Environment.UserName // 현재 Windows 사용자 이름
            };

            if (!NativeMethods.CredWrite(ref credential, 0))
            {
                var error = Marshal.GetLastWin32Error();
                // TODO: 윈도우 오류 로깅 필요 (Rule: 빈 catch 지양)
                // 현재는 콘솔이나 디버그 출력을 대신함
                System.Diagnostics.Debug.WriteLine($"Failed to write credential: {error}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    public string ReadCredential(string key)
    {
        var targetName = TargetPrefix + key;
        if (NativeMethods.CredRead(targetName, NativeMethods.CRED_TYPE_GENERIC, 0, out var credPtr))
        {
            try
            {
                var cred = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(credPtr);
                var blob = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
                return Encoding.Unicode.GetString(blob);
            }
            finally
            {
                NativeMethods.CredFree(credPtr);
            }
        }

        return string.Empty;
    }

    public void DeleteCredential(string key)
    {
        var targetName = TargetPrefix + key;
        NativeMethods.CredDelete(targetName, NativeMethods.CRED_TYPE_GENERIC, 0);
    }
}
