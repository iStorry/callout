# Code signing setup (SignPath Foundation)

Releases are built unsigned until these steps are done. The release workflow
([.github/workflows/release.yml](../.github/workflows/release.yml)) already contains the signing
steps; they switch on by themselves once the repository variable below exists.

## 1. Apply

Apply for free open-source signing at https://signpath.org/apply with the repository
`https://github.com/iStorry/callout`. SignPath Foundation reviews the project. Their conditions
include an OSI-approved license (add a `LICENSE` file before applying) and the code signing
policy section that is already in the README.

## 2. Configure SignPath (after approval)

In the SignPath web app:

1. **Project:** create a project with slug `callout`, linked to the GitHub repository.
2. **Trusted build system:** link GitHub.com to the project, so only builds from this repository's
   GitHub Actions can request signatures.
3. **Artifact configurations:** create two, pasting the files from this repo:
   - slug `app`: [.signpath/artifact-configurations/app.xml](../.signpath/artifact-configurations/app.xml)
     (signs `Callout.exe`, `Callout.dll`, `Callout.Core.dll`)
   - slug `installer`: [.signpath/artifact-configurations/installer.xml](../.signpath/artifact-configurations/installer.xml)
     (signs `Callout-Setup-x64.exe` / `Callout-Setup-arm64.exe`)
4. **Signing policy:** slug `release-signing`, using the certificate SignPath Foundation issues.
   If the policy requires manual approval, each release waits until you approve it in SignPath
   (the workflow waits up to 10 minutes per request).
5. **API token:** create a CI user with permission to submit signing requests for this policy,
   and copy its API token.

If you pick different slugs, change `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG` or the
`artifact-configuration-slug` values in the workflow to match.

## 3. Configure GitHub

In the repository: **Settings → Secrets and variables → Actions**

| Kind | Name | Value |
|---|---|---|
| Secret | `SIGNPATH_API_TOKEN` | the CI user's API token |
| Variable | `SIGNPATH_ORGANIZATION_ID` | your SignPath organization ID (shown in SignPath's organization settings) |

Setting the variable is what turns signing on.

## 4. Verify

Run **Actions → Release → Run workflow** (a dry run that publishes nothing). Download the
`release-x64` artifact and check on Windows: right-click `Callout-Setup-x64.exe` → **Properties →
Digital Signatures** should list the signer. Do the same for `Callout.exe` inside the zip. Then tag
a release as usual.

## How the workflow signs

1. `app` job: publishes each architecture, uploads it, and SignPath signs Callout's own binaries.
2. `package` job: builds the portable zip and the Inno Setup installer from the **signed** app,
   then SignPath signs the installer.
3. `publish` job: attaches the signed installers and zips to the GitHub release.

New signatures still need SmartScreen reputation, so the warning can appear for a short while
after the first signed release.
