using System.ComponentModel.DataAnnotations;

namespace SwiftBets.Originals.Infrastructure;

public sealed class OriginalsOptions
{
    public const string SectionName = "Originals";

    /// <summary>The casino gateway, where the seamless-wallet API lives.</summary>
    [Required]
    public string CasinoAddress { get; set; } = string.Empty;

    /// <summary>The signing secret the casino holds for provider <see cref="ProviderId"/>.</summary>
    [Required]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    public string ProviderId { get; set; } = "swift-originals";

    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "ZAR";
}
