using System;

namespace PackageManager.Alpm;

[Flags]
internal enum AlpmSigLevel : uint
{
    // Package settings
    Package = (1 << 0),            // Packages require a signature
    PackageOptional = (1 << 1),    // Check signatures if they exist
    PackageMarginalOk = (1 << 2),  // Accept marginal trust
    PackageUnknownOk = (1 << 3),   // Accept unknown trust

    // Database settings
    Database = (1 << 10),           // Databases require a signature
    DatabaseOptional = (1 << 11),   // Check signatures if they exist
    DatabaseMarginalOk = (1 << 12), // Accept marginal trust
    DatabaseUnknownOk = (1 << 13),  // Accept unknown trust

    // Meta settings
    UseDefault = (1 << 30),         // Use the global default level
    None = 0                        // No verification
}