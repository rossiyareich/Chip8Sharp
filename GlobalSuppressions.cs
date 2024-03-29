﻿// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly:
    SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member",
        Target = "~M:Chip8Sharp.Program.Main")]
[assembly:
    SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member",
        Target = "~M:Chip8Sharp.CPU.DrawDisplaySDL")]
[assembly: SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>", Scope = "member", Target = "~M:Chip8Sharp.CPU.Dispose")]
