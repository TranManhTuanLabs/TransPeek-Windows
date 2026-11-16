# Source notes — read 2026-11-15/16 (UTC+7, mission date 2026-11-15)

## Official Microsoft Learn — verified live via browser_open 2026-11-15
- Code signing options: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
  Store MSIX re-signed free; MSI/EXE Store must be pre-signed, self-signed not accepted; Azure Artifact Signing ~$9.99/mo, orgs USA/Canada/EU/UK, individuals USA/Canada only; OV $150-300/yr; EV $400+/yr, no longer SmartScreen bypass; self-signed dev/testing only, blocks public install; SignPath Foundation free OSS OV-level.
- SmartScreen reputation: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation
  Store = no warning; OV/EV = warning until reputation accumulates; no signature / self-signed = same "Windows protected your PC", Run anyway; reputation = publisher + file-hash, several weeks + hundreds of clean installs, no exact threshold, no manual review for consumers; Smart App Control may block unsigned on Win11.
- Sign an MSIX package: https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview
  MSIX must be signed+trusted; Azure Basic ~$10/mo, OV $300-500/yr (table differs from code-signing page $150-300 — vendor variance), Store free; eligibility Public Trust orgs USA/Canada/EU/UK, individuals USA/Canada, orgs 3+ yr tax history; short-lived certs ~3 days, timestamping required.
- Free individual Store registration: https://learn.microsoft.com/en-us/windows/apps/publish/whats-new-individual-developer
  $19 waived, nearly 200 markets, gov ID + selfie, entry only via storedeveloper.microsoft.com; other paths show legacy flow.
- Account types/fees (search result): https://learn.microsoft.com/en-us/windows/apps/publish/partner-center/account-types-locations-and-fees — no registration fees for either type in new onboarding.
- Packaging overview / unpackaged WinUI: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/ and https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app
  Packaged MSIX = identity, Store eligible, auto updates; unpackaged = no identity, no MSIX Store submission, but MSI/EXE Store submission possible with silent install; PublishSingleFile only unpackaged+self-contained, WinAppSDK 1.5+.
- Publish first app: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/publish-first-app — Store re-signs MSIX, MSI/EXE must be signed; certification 1-3 business days (also https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msi/app-certification-process — up to 3 business days, listing visible ~15 min after).
- Store Policies v7.20, pub 2026-09-15, eff 2026-10-22: https://learn.microsoft.com/mt-mt/windows/apps/publish/store-policies — 10.1 distinct function/value, 10.2 security/no malware, 10.2.5 updates only through Store for Store products.
- SpeechSynthesizer: https://learn.microsoft.com/en-us/uwp/api/windows.media.speechsynthesis.speechsynthesizer?view=winrt-22621 — only Microsoft-signed installed voices, AllVoices/DefaultVoice, SynthesizeTextToStreamAsync/SSML.
- Windows App SDK release channels: https://learn.microsoft.com/et-ee/windows/apps/windows-app-sdk/release-channels — 1.8 end of servicing 2026-09-24, 2.5.1 stable 2026-09-16.

## Velopack official — verified live
- https://docs.velopack.io/ — installer + auto-update, zero config, C# ready, migration from Squirrel/ClickOnce.
- https://docs.velopack.io/getting-started/csharp — VelopackApp.Build().Run() first in Main; UpdateManager Check/Download/ApplyUpdatesAndRestart; vpk dotnet tool, vpk pack --packId --packVersion --packDir --mainExe.
- https://docs.velopack.io/integrating/overview — GithubSource("https://github.com/yourName/yourRepo", null, false), CheckForUpdatesAsync reads releases.{channel}.json, DownloadUpdatesAsync downloads deltas, silent or UI, SetAutoApplyOnStartup default on.
- Velopack 1.2.x in community examples (2026); SemVer2 only for packVersion, no 4-part version (skill mirror https://github.com/hinoderun/velopack-skill/blob/HEAD/skills/velopack/SKILL.md).

## SignPath — official + community
- https://about.signpath.io/product/open-source — free cert for OSS, build must be fully automated/integrated, binary built only from checked-in source.
- https://learn.microsoft.com code-signing page above confirms SignPath Foundation free OSS.
- Eligibility/process (secondary, consistent across multiple OSS repos): OSI-approved licence, public repo, automated GitHub Actions build, already released in form to be signed, no malware/PUP, code-signing policy published, MFA; publisher name shown is "SignPath Foundation", cert from Sectigo, stored in SignPath HSM, never leaves it.
- GitHub Action: signpath/github-action-submit-signing-request@v2, SIGNPATH_API_TOKEN secret, org/project/policy slugs as variables, artifact built by GitHub Actions, human approver must approve every Foundation release (per https://github.com/oldkrab/openaide/blob/HEAD/docs/research/signpath-windows-onboarding-2026-08-31.md and https://github.com/niivue/bidsvue/blob/HEAD/signpath.md).
- Caveats: one secondary report said applications temporarily paused for backlog (unverified, date unclear); another reported rejection for low stars/downloads (anecdotal, not an official criterion) — flag as check-at-apply-time, do not present as official rule.

## Pricing — secondary, checked Oct-Nov 2026
- Azure Artifact Signing: Basic $9.99/mo / 5,000 sigs, Premium $99.99/mo / 100,000, $0.005 over quota = $119.88/yr Basic. Sources: https://azure.microsoft.com/en-us/pricing/details/artifact-signing/ (via search), https://weblog.west-wind.com/posts/2025/Jul/20/Fighting-through-Setting-up-Microsoft-Trusted-Signing, https://dawloom.com/blog/code-signing-certificate-cost/
- OV/EV reseller variance: SSL Dragon https://www.ssldragon.com/ssl-certificates/code-signing/ — Comodo/Sectigo OV from $219/yr, Sectigo EV $287/yr, DigiCert OV $400/yr, EV $685/yr; SSL.com OV $129/yr (1yr) per Dawloom checked 2026-10-13; Sectigo direct from ~$536/yr on 5-yr, DigiCert direct $696 OV / $972 EV (12-mo) per https://www.codenote.net/en/posts/ov-ev-code-signing-certificate-pricing-japan-2026/ . Microsoft Learn's $150-300 OV / $400+ EV is a guide band, not a quote. Max validity 460 days from 2026-03-01 (CA/B CSC-31), HSM/token mandatory since 2023-06-01.
- Azure eligibility conflict: Learn (opened 2026-11-15) says orgs USA/Canada/EU/UK, individuals USA/Canada. Secondary https://my-ssl.com/learn/azure-trusted-signing-vs-code-signing-certificate (Nov 2026) claims orgs expanded to 12 places incl AU/NZ/JP/KR/SG/CH/NO/IL, individuals still USA/Canada only. Either way Vietnam individual is excluded. Do not rely on expanded list.

## Installer comparison
- WiX v6 OSMF: https://github.com/dariusz-wozniak/fossed/blob/HEAD/content/libraries/wix-toolset.mdx — v6 Apr 2025, fee for revenue orgs >~$10k/yr, hobbyist/non-revenue free; v5 free, ageing.
- Squirrel/Clowd.Squirrel deprecated: NuGet Clowd.Squirrel 2.11.1 deprecated "no longer maintained", last release 2023-11-14; Squirrel.Windows last release Sep 2020 per https://github.com/electron/forge/issues/3598. Velopack is successor, offers migration.
- Inno/NSIS: free, no built-in auto-update, script-driven; Inno ~2-3h learning, WiX 1-2 weeks per secondary guides.

## TTS
- Edge TTS unofficial: https://freeapihub.com/blog/best-free-text-to-speech-apis — edge-tts Python, no key, 300+ voices, Oct 2026 token change broke <=7.2.x, >=7.3.1 works; no SLA, do not build commercial product on it. https://github.com/libbyyounh/openmontage/blob/HEAD/.agents/skills/edge-tts/SKILL.md — same, needs network, unofficial endpoint.
- Edge endpoint: wss://speech.platform.bing.com, Sec-MS-GEC token, reverse-engineered, per https://github.com/awais786/generative-ai-vdos/blob/HEAD/.claude/skills/voiceover-tts/SKILL.md and OmniRoute PR https://github.com/cheaper-inference/OmniRoute/pull/7605.
- C# port: https://www.nuget.org/packages/EdgeTTS.DotNet — 0.4.0, MIT, feature-complete migration of Python edge-tts, targets net9.0/net10.0 (no net8.0 listed) — check .NET 8 compat before adopting.
- System voices: https://support.microsoft.com/en-us/accessibility/windows/narrator/appendix-a-supported-languages-and-voices — en-US David/Mark/Zira, Vietnamese An (male); extra voices need language-pack install (Settings > Language, Text-to-Speech feature). Natural voices (Guy/Jenny/Aria en-US etc) are Narrator/OneCore, not all exposed via SpeechSynthesizer.
- Azure Speech official: F0 500,000 neural chars/mo free, never expires, 20 req/min; pay-as-you-go $15-16/1M neural, $22 HD — https://www.cartesia.ai/learn/top-microsoft-azure-text-to-speech-alternatives (checked 2026-10-05 vs Azure pricing), https://azure.microsoft.com/en-us/pricing/details/speech/ . Needs Azure account + key/region; embedding a shared key in an OSS client is not viable — per-user key or own proxy required.
- WinUI 3 + Velopack Main: template https://github.com/fettah010/winui-3-easy-template/commit/156c511c3a230bb9b4f35596e75109849175c51f uses DISABLE_XAML_GENERATED_MAIN + explicit Program.Main with Velopack 1.2.0, net10.0.
