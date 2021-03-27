﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetCheck.Models;
using DotNetCheck.Manifest;
using NuGet.Versioning;
using DotNetCheck.Solutions;

namespace DotNetCheck.Checkups
{
	public class AndroidEmulatorCheckup : Checkup
	{
		public override IEnumerable<CheckupDependency> DeclareDependencies(IEnumerable<string> checkupIds)
			=> new [] { new CheckupDependency("androidsdk") };

		public IEnumerable<AndroidEmulator> RequiredEmulators
			=> Manifest?.Check?.Android?.Emulators;

		public override string Id => "androidemulator";

		public override string Title => "Android Emulator";

		public override bool ShouldExamine(SharedState history)
			=> RequiredEmulators?.Any() ?? false;

		public override Task<DiagnosticResult> Examine(SharedState history)
		{
			var android = new AndroidSdk.AndroidSdkManager(
				history.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? history.GetEnvironmentVariable("ANDROID_HOME"));

			var avds = AndroidSdk.AvdManager.ListAvdsFromFiles();

			if (!avds.Any())
				avds = android.AvdManager.ListAvds();

			if (avds.Any())
			{
				var emu = avds.FirstOrDefault();

				ReportStatus($"Emulator: {emu.Name ?? emu.SdkId} found.", Status.Ok);

				return Task.FromResult(DiagnosticResult.Ok(this));
			}

			var missingEmulators = new List<AndroidEmulator>();

			foreach (var emu in RequiredEmulators)
			{
				var existingAvd = avds.FirstOrDefault(a => a.SdkId.Equals(emu.SdkId, StringComparison.OrdinalIgnoreCase));

				if (existingAvd == null)
				{
					missingEmulators.Add(emu);
					ReportStatus($"{emu.Description ?? emu.SdkId} not created.", Status.Error);
				}
				else
				{
					ReportStatus($"{emu.Description ?? emu.SdkId} exists.", Status.Ok);
				}

			}

			if (!missingEmulators.Any())
				return Task.FromResult(DiagnosticResult.Ok(this));

			var devices = android.AvdManager.ListDevices();

			var preferredDevice = devices.FirstOrDefault(d => d.Name.Contains("pixel", StringComparison.OrdinalIgnoreCase));

			return Task.FromResult(new DiagnosticResult(
				Status.Error,
				this,
				new Suggestion("Create an Android Emulator",
					missingEmulators.Select(me =>
						new ActionSolution(t =>
						{
							android.AvdManager.Create($"Android_Emulator_{me.ApiLevel}", me.SdkId, device: preferredDevice?.Id, tag: "google_apis", force: true, interactive: true);
							return Task.CompletedTask;
						})).ToArray())));
		}
	}

}
