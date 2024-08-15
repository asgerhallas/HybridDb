param (
    [Parameter(Mandatory=$true)][string]$BuildNumber
)

$ErrorActionPreference = "Stop";

$git_describe = (git describe --tags) 
$git_describe_long = (git describe --tags --long) 

if ($null -eq $git_describe) {
  $git_describe_long = $git_describe = "0.0.1-0"
}

$head_is_tagged = $git_describe -ne $git_describe_long

if ($head_is_tagged)
{
  # output the tag as is
  Write-Output $git_describe
  exit;
}

# output the description without the trailing commit hash
$match = ($git_describe | Select-String -pattern '(?<version>.+?)-(?<commit_number>[^-]+)-(?<commit_hash>[^-]+)$').Matches[0].Groups

Write-Output $($match['version'].Value + "-dev." + $match['commit_number'].Value + "." + $match['commit_hash'].Value + "." + $BuildNumber)
