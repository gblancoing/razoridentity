namespace ComunaClick.Api.Modules.Marketplace;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string cipherText);
}
