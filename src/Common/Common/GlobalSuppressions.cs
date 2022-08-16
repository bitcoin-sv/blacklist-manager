// Copyright (c) 2020 Bitcoin Association

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "With .NET Core there are no issues when awaiting foreign tasks", Scope = "member", Target = "~M:Common.BackgroundTask.CancelTaskAsync~System.Threading.Tasks.Task")]
