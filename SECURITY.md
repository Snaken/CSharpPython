# Security policy

CSharpPython runs scripts that may come from players, so reports about a script escaping its limits are taken seriously. Examples: a script that hangs or crashes the host despite `MaxSteps`, `MaxCallDepth` or `MaxIntBits`; memory use that grows without bound; a crafted snapshot that does anything other than raise `SnapshotException`; or any way for a script to reach something the host did not register.

## Reporting a vulnerability

Please do not open a public issue. Use GitHub's private reporting: on the repository's **Security** tab choose **Report a vulnerability**. Include a small script (or snapshot) that shows the problem, the version, and what you expected to happen.

You can expect a first reply within a week. Fixes are released as a new version and credited in the changelog unless you prefer otherwise.

## Supported versions

Only the latest release is supported.
