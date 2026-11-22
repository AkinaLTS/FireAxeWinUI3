# Source: original P7 validation using .NET System.Reflection.PortableExecutable.PEReader.
# PE import descriptors follow https://learn.microsoft.com/windows/win32/debug/pe-format#import-directory-table
# PEReader supplies headers and RVA mapping but has no public import-table enumerator.
# No application code is executed. Static imports do not cover delay-load, COM, or dynamic loads.
[CmdletBinding()]
param([Parameter(Mandatory)][string] $PublishDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Dependency resolution must be checked on Windows.' }
$directory = (Resolve-Path $PublishDirectory).Path
if (-not ('FireAxe.Publishing.NativeImports' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
namespace FireAxe.Publishing;
public static class NativeImports
{
    public static string[] Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var table = pe.PEHeaders.PEHeader.ImportTableDirectory;
        if (table.RelativeVirtualAddress == 0) return Array.Empty<string>();
        var imports = new List<string>();
        var descriptors = pe.GetSectionData(table.RelativeVirtualAddress).GetReader();
        while (descriptors.RemainingBytes >= 20)
        {
            int lookup = descriptors.ReadInt32();
            int timestamp = descriptors.ReadInt32();
            int forwarder = descriptors.ReadInt32();
            int nameRva = descriptors.ReadInt32();
            int address = descriptors.ReadInt32();
            if ((lookup | timestamp | forwarder | nameRva | address) == 0) break;
            var name = pe.GetSectionData(nameRva).GetReader();
            var bytes = new List<byte>();
            while (name.RemainingBytes > 0)
            {
                byte value = name.ReadByte();
                if (value == 0) break;
                bytes.Add(value);
            }
            imports.Add(Encoding.ASCII.GetString(bytes.ToArray()));
        }
        return imports.ToArray();
    }
}
'@
}
$checks = foreach ($file in (Get-ChildItem $directory -File | Where-Object Extension -In '.dll', '.exe')) {
    foreach ($dependency in [FireAxe.Publishing.NativeImports]::Read($file.FullName)) {
        $resolution = if (Test-Path (Join-Path $directory $dependency) -PathType Leaf) { 'app-local' }
            elseif ($dependency -match '^(api-ms-|ext-ms-)') { 'Windows API set' }
            elseif (Test-Path (Join-Path $env:SystemRoot "System32/$dependency") -PathType Leaf) { 'System32 on validation host' }
            else { 'MISSING' }
        [pscustomobject]@{ File = $file.Name; Dependency = $dependency; Resolution = $resolution }
    }
}
$checks
$missing = @($checks | Where-Object Resolution -EQ 'MISSING')
if ($missing.Count) { throw "Unresolved static native dependencies: $($missing.Dependency -join ', ')" }
