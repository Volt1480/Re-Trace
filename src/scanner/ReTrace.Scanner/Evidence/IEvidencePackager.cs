using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Evidence;

/// <summary>
/// Packages scan results and evidence into exportable formats (JSON + ZIP).
/// </summary>
public interface IEvidencePackager
{
    /// <summary>
    /// Exports the scan result to JSON and optionally packages evidence files into a ZIP.
    /// Returns the path to the output directory.
    /// </summary>
    string Package(ScanResult result, string outputPath, bool includeZip);
}
