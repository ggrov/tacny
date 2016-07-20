﻿// PkgCmdID.cs
// MUST match PkgCmdID.h
using System;

namespace DafnyLanguage.DafnyMenu
{
  static class PkgCmdIDList
  {
    public const uint cmdidCompile = 0x100;
    public const uint cmdidRunVerifier = 0x101;
    public const uint cmdidStopVerifier = 0x102;
    public const uint cmdidMenu = 0x1021;
    public static uint cmdidToggleSnapshotVerification = 0x103;
    public const uint cmdidToggleBVD = 0x104;
    public static uint cmdidToggleMoreAdvancedSnapshotVerification = 0x105;
    public static uint cmdidToggleAutomaticInduction = 0x106;
    public static uint cmdidDiagnoseTimeouts = 0x107;
    
    public const int ExpandTacticsCommandId = 0x0108;
    public const int ExpandAllTacticsCommandId = 0x0109;
    public const int ToggleTacnyCommandId = 0x0110;
    
    public const int ContextExpandTacticsCommandId = 0x0140;
  };
}