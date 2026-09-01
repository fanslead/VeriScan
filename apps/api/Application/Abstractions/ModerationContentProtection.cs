namespace VeriScan.Application.Abstractions;

public interface IModerationContentProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedContent);
}
