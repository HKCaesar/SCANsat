﻿using System;
using System.Collections.Generic;
using System.Linq;
using SCANsat.SCAN_Data;


namespace SCANsat.SCAN_PartModules
{
	class SCANresourceDisplay : ModuleResourceScanner, IAnimatedModule
	{
		[KSPField]
		public int sensorType;

		private List<ModuleResourceScanner> stockScanners;
		private Dictionary<string, ResourceCache.AbundanceSummary> abundanceSummary;
		private CelestialBody body;
		private bool tooHigh;
		private bool fuzzy;
		private bool forceStart;

		public override void OnStart(PartModule.StartState state)
		{
			if (state == StartState.Editor)
				return;

			GameEvents.onVesselSOIChanged.Add(onSOIChange);

			this.enabled = true;
			forceStart = true;

			SCANUtil.SCANlog("Resource Display Module [{0}] Starting...", ResourceName);

			stockScanners = findScanners();

			setupFields(stockScanners.FirstOrDefault());

			body = FlightGlobals.currentMainBody;
			refreshAbundance(body.flightGlobalsIndex);
		}

		private List<ModuleResourceScanner> findScanners()
		{
			return part.FindModulesImplementing<ModuleResourceScanner>().Where(r => r.ScannerType == 0 && r.ResourceName == ResourceName).ToList();
		}

		private void setupFields(ModuleResourceScanner m)
		{
			if (m != null)
			{
				SCANUtil.SCANlog("Resource Display Module set to Max Alt: {0} ; Unlock: {1}", m.MaxAbundanceAltitude, m.RequiresUnlock);
				MaxAbundanceAltitude = m.MaxAbundanceAltitude;
				RequiresUnlock = m.RequiresUnlock;
			}
			else
			{
				MaxAbundanceAltitude = 250000;
				RequiresUnlock = true;
			}

			Fields["abundanceDisplay"].guiName = string.Format("{0}[Surf]: ", ResourceName);
		}

		private void OnDestroy()
		{
			GameEvents.onVesselSOIChanged.Remove(onSOIChange);
		}

		public override void OnUpdate()
		{
			SCANUtil.SCANlog("Updating Resource Module...");
			if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready)
				return;
			SCANUtil.SCANlog("Scene Ready...");
			if (forceStart && SCANcontroller.controller != null)
			{
				if (stockScanners != null && SCANcontroller.controller.disableStockResource)
				{
					foreach (ModuleResourceScanner m in stockScanners)
					{
						m.DisableModule();
					}
				}
			}
			SCANUtil.SCANlog("Checking for stock resource options...");
			if (!SCANcontroller.controller.disableStockResource)
			{
				Fields["abundanceDisplay"].guiActive = false;
				return;
			}
			SCANUtil.SCANlog("Setting SCAN Resource Display Active");
			Fields["abundanceDisplay"].guiActive = true;

			if (tooHigh)
			{
				abundanceDisplay = "Too High";
				return;
			}
			else if (abundanceValue < 0)
			{
				abundanceDisplay = "No Data";
				return;
			}

			string biome = "Landed";

			if (body.BiomeMap != null)
				biome = SCANUtil.getBiomeName(body, SCANUtil.fixLonShift(vessel.longitude), SCANUtil.fixLatShift(vessel.latitude));

			if (checkBiome(biome) || !SCANcontroller.controller.resourceBiomeLock)
			{
				if (fuzzy)
					abundanceDisplay = abundanceValue.ToString("P0");
				else
					abundanceDisplay = abundanceValue.ToString("P2");
			}
			else
			{
				float biomeAbundance = abundanceSummary.ContainsKey(biome) ? abundanceSummary[biome].Abundance : 0f;
				if (fuzzy)
					abundanceDisplay = biomeAbundance.ToString("P0");
				else
					abundanceDisplay = biomeAbundance.ToString("P2");
			}
		}

		private bool checkBiome (string b)
		{
			return ResourceMap.Instance.IsBiomeUnlocked(body.flightGlobalsIndex, b);
		}

		public override void OnFixedUpdate()
		{
			if (vessel.altitude > MaxAbundanceAltitude)
			{
				tooHigh = true;
				return;
			}

			tooHigh = false;
			double lat = SCANUtil.fixLatShift(vessel.latitude);
			double lon = SCANUtil.fixLonShift(vessel.longitude);
			if (SCANUtil.isCovered(lon, lat, vessel.mainBody, sensorType))
			{
				abundanceValue = SCANUtil.ResourceOverlay(lat, lon, ResourceName, vessel.mainBody, RequiresUnlock && SCANcontroller.controller.resourceBiomeLock);
				fuzzy = false;
			}
			else if (SCANUtil.isCovered(lon, lat, vessel.mainBody, 524288))
			{
				abundanceValue = SCANUtil.ResourceOverlay(lat, lon, ResourceName, vessel.mainBody, RequiresUnlock && SCANcontroller.controller.resourceBiomeLock);
				fuzzy = true;
			}
			else
			{
				abundanceValue = -1;
			}
		}

		private void onSOIChange(GameEvents.HostedFromToAction<Vessel, CelestialBody> VB)
		{
			body = VB.to;
			refreshAbundance(body.flightGlobalsIndex);
		}

		private void refreshAbundance(int bodyID)
		{
			abundanceSummary = new Dictionary<string, ResourceCache.AbundanceSummary>();

			abundanceSummary = ResourceCache.Instance.AbundanceCache.
				Where(a => a.ResourceName == ResourceName && a.HarvestType == HarvestTypes.Planetary && a.BodyId == bodyID).
				ToDictionary(a => a.BiomeName, a => a);
		}

		void IAnimatedModule.EnableModule()
		{
			this.enabled = true;
			SCANUtil.SCANlog("Enabling Resource Module");
			if (stockScanners != null && SCANcontroller.controller != null && SCANcontroller.controller.disableStockResource)
			{
				foreach (ModuleResourceScanner m in stockScanners)
				{
					m.DisableModule();
				}
			}
		}

		void IAnimatedModule.DisableModule()
		{
			this.enabled = false;
			SCANUtil.SCANlog("Disabling Resource Module");
			if (stockScanners != null && SCANcontroller.controller != null && SCANcontroller.controller.disableStockResource)
			{
				foreach (ModuleResourceScanner m in stockScanners)
				{
					m.DisableModule();
				}
			}
		}

		bool IAnimatedModule.ModuleIsActive()
		{
			return isEnabled;
		}

		bool IAnimatedModule.IsSituationValid()
		{
			return true;
		}
	}
}
