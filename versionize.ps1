$git_describe = (git describe --tags --long) 

if ($git_describe -eq $null) {
  $git_describe = "0.0.1-0"
}

$git_describe = ($git_describe | Select-String -pattern '(?<major>[0-9]+)\.(?<minor>[0-9]+).(?<patch>[0-9]+)-(?<commitcount>[0-9]+)').Matches[0].Groups

$majorVersion = $git_describe['major'].Value
$minorVersion = $git_describe['minor'].Value -as [int]
$patchVersion = $git_describe['patch'].Value -as [int]
$commit_count = $git_describe['commitcount'].Value -as [int]

$current_branch = (git rev-parse --abbrev-ref HEAD)
$nextPatchVersion = $patchVersion + $commit_count

If ($nextPatchVersion -gt 9999)
{
  $minorVersion++
  $nextPatchVersion = 0
}

$projectVersion = $majorVersion + "." + $minorVersion
$projectVersion += "." + ($patchVersion + $commit_count)
$packageVersion = $projectVersion

If ($current_branch -ne "master") 
{
  $packageVersion = $majorVersion + "." + $minorVersion + "." + ($patchVersion + $commit_count) + "-" + $current_branch
}

if (Get-Command Update-AppveyorBuild -errorAction SilentlyContinue)
{
    Update-AppveyorBuild -Version $packageVersion
}
else
{
    Write-Host $packageVersion
}

# Write-Host "##teamcity[setParameter name='ProjectVersion' value='$projectVersion']"
# Write-Host "##teamcity[setParameter name='PackageVersion' value='$packageVersion']"
# Write-Host "##teamcity[setParameter name='GitVersion' value='$packageVersion']"]]