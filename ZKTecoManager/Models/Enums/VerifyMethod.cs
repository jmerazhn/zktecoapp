namespace ZKTecoManager.Models.Enums;

public enum VerifyMethod : byte
{
    Fingerprint = 1,
    Password = 3,
    Card = 4,
    Face = 15,
    FingerprintAndCard = 200
}
