$msbuild = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"

function Create-OctopusToolsRelease ([string]$BuildNumber="1.0.0.0")
{
    <#
    .Synopsis
     Builds a complete release (compile, test, ILMerge, NuGet package, version stamp)
    #>
    Write-Host "Build number: $BuildNumber"
	& $msbuild "Tools\Build.proj" /p:build_number=$BuildNumber /t:BuildAndPublish
}

function Create-OctopusToolsPackage
{
    <#
    .Synopsis
        Compiles, tests, runs ILMerge
    #>
	& $msbuild "Tools\Build.proj" /t:BuildAndPackage /v:q
}

Export-ModuleMember -function * -alias *
