// Source: https://github.com/Team-Sellora/sellora-organization/blob/9772eb48d4a05a6a037c408e5c5ab850ba5f2fbe/src/Sellora.CoreService.Infrastructure/IdentityProvisioning/TemporaryPasswordGenerator.cs
// Copied unchanged for reference. Team-Sellora, SLIIT SE3022.

using System.Security.Cryptography;

namespace Sellora.CoreService.Infrastructure.IdentityProvisioning;

/// <summary>
/// 16 random characters with at least one upper, lower, digit and symbol,
/// which satisfies WSO2 IS's default password policy.
/// </summary>
public static class TemporaryPasswordGenerator
{
  private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
  private const string Lower = "abcdefghijkmnpqrstuvwxyz";
  private const string Digits = "23456789";
  private const string Symbols = "!@#$%*?";

  public static string Generate(int length = 16)
  {
    var all = Upper + Lower + Digits + Symbols;
    var characters = new List<char>
    {
      Pick(Upper), Pick(Lower), Pick(Digits), Pick(Symbols)
    };

    while (characters.Count < length)
    {
      characters.Add(Pick(all));
    }

    // Shuffle so the guaranteed characters are not always first.
    for (var i = characters.Count - 1; i > 0; i--)
    {
      var j = RandomNumberGenerator.GetInt32(i + 1);
      (characters[i], characters[j]) = (characters[j], characters[i]);
    }

    return new string(characters.ToArray());
  }

  private static char Pick(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];
}
