// Barcode/QR payload routing (mac parity: AppDelegate.performScanBarcodes
// + TranslationCoordinator.scanBarcodes). Pure logic, no WinRT, so it
// runs on Linux CI too.
//
//   URL (http/https) -> open in the default browser (never auto-open
//     anything else: phone numbers, bank accounts, plain text, Wi-Fi
//     strings and vCards are only ever copied).
//   Anything else non-empty -> copy to the clipboard, toast a preview.
//   Empty / whitespace -> nothing found.

namespace TransPeek.Core.Barcode;

public enum BarcodePayloadAction
{
    OpenUrl,
    Copy,
    NotFound,
}

public static class BarcodePayload
{
    /// <summary>Short preview for the copy toast: long payloads (e.g.
    /// vCards) are truncated so the toast stays short. Mac parity:
    /// 80 chars + an ellipsis.</summary>
    public const int PreviewMaxLength = 80;

    public static BarcodePayloadAction Classify(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return BarcodePayloadAction.NotFound;
        return TryGetUrl(payload, out _) ? BarcodePayloadAction.OpenUrl : BarcodePayloadAction.Copy;
    }

    public static bool TryGetUrl(string payload, out Uri url)
    {
        url = null!;
        if (!Uri.TryCreate(payload.Trim(), UriKind.Absolute, out var parsed))
            return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            return false;
        url = parsed;
        return true;
    }

    public static string Preview(string payload)
    {
        if (payload.Length <= PreviewMaxLength)
            return payload;
        return string.Concat(payload.AsSpan(0, PreviewMaxLength), "…");
    }
}
