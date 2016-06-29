using System;

namespace DafnyLanguage.TacnyLanguage
{
  public static class TacnyPackageIdentifiers
  {
    public const string PackageGuidString = "f982f63a-aa1f-421d-9400-f3c10896146b";
    public const string CommandSetGuidString = "6800196a-f13c-4bd6-a795-456a2eb74164";

    public static readonly Guid PackageGuid = new Guid(PackageGuidString);
    public static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);

    public const int ExpandTacticsCommandId = 0x0100;
    public const int ExpandAllTacticsCommandId = 0x0101;
    public const int DisableTacnyCommandId = 0x0102;
  }
}