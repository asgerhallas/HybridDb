$ErrorActionPreference = "Stop";

$git_describe = (git describe --tags) 
$git_describe_long = (git describe --tags --long) 

Write-Host $git_describe
Write-Host $git_describe_long

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
$match = ($git_describe | Select-String -pattern '(?<version>.+?)(-[^-]+)$').Matches[0].Groups

Write-Output $match['version'].Value