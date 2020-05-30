﻿using AntimatterAnnihilation.Utils;
using RimWorld;
using Verse;

namespace AntimatterAnnihilation.Buildings
{
    public class Building_CompositeRefiner : Building_TrayPuller, IConditionalGlower
    {
        public bool ShouldBeGlowingNow
        {
            get
            {
                return GetShouldBeRunning();
            }
        }

        public CompPowerTrader PowerTraderComp
        {
            get
            {
                if (_powerTraderComp == null)
                    this._powerTraderComp = base.GetComp<CompPowerTrader>();
                return _powerTraderComp;
            }
        }
        private CompPowerTrader _powerTraderComp;
        public CompGlower CompGlower
        {
            get
            {
                if (_compGlower == null)
                    this._compGlower = base.GetComp<CompGlower>();
                return _compGlower;
            }
        }
        private CompGlower _compGlower;
        public IntVec3 OutputPos
        {
            get
            {
                IntVec3 pos = this.Position + new IntVec3(0, 0, -1);
                return pos;
            }
        }

        public int MissingPlasteel
        {
            get
            {
                return MaxPlasteel - CurrentPlasteelCount;
            }
        }
        public int MissingAntimatter
        {
            get
            {
                return MaxAntimatter - CurrentAntimatterCount;
            }
        }

        public int TicksToProduceOutput = 15000; // 6 in-game hours.
        public int MaxPlasteel = 60;
        public int MaxAntimatter = 1;
        public int OutputAmount = 30;

        public int CurrentPlasteelCount;
        public int CurrentAntimatterCount;
        public int ProductionTicks;

        private ulong tickCount;
        private bool lastFrameRunning;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                CreateZone();
            }
        }

        private void CreateZone()
        {
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Map.zoneManager);
            zone.settings.filter.SetDisallowAll();
            zone.settings.Priority = StoragePriority.Low;
            zone.settings.filter.SetAllow(AADefOf.AntimatterComposite_AA, true);
            Map.zoneManager.RegisterZone(zone);
            zone.AddCell(OutputPos);
        }

        public override void Tick()
        {
            base.Tick();

            tickCount++;

            bool isRunning = GetShouldBeRunning();
            if (isRunning)
            {
                ProductionTicks++;
                if (ProductionTicks >= TicksToProduceOutput)
                {
                    ProductionTicks = 0;
                    CurrentAntimatterCount = 0;
                    CurrentPlasteelCount = 0;

                    PlaceOutput(OutputAmount);
                }
            }

            if (lastFrameRunning != isRunning)
            {
                CompGlower?.ReceiveCompSignal("PowerTurnedOn"); // Obviously the power hasn't actually just been turned on, but it's just a way to trigger UpdateLit to be called.
            }
            lastFrameRunning = isRunning;

            if (tickCount % 120 == 0 && PowerTraderComp.PowerOn)
            {
                Building_InputTray lt = null;
                Building_InputTray rt = null;
                if (MissingPlasteel != 0)
                {
                    lt = GetLeftTray();
                    rt = GetRightTray();

                    CurrentPlasteelCount += TryPullFromTray(rt, "Plasteel", MissingPlasteel);
                    CurrentPlasteelCount += TryPullFromTray(lt, "Plasteel", MissingPlasteel);
                }

                if (MissingAntimatter != 0)
                {
                    if(lt == null)
                    {
                        lt = GetLeftTray();
                        rt = GetRightTray();
                    }

                    CurrentAntimatterCount += TryPullFromTray(lt, "AntimatterCanister_AA", MissingAntimatter);
                    CurrentAntimatterCount += TryPullFromTray(rt, "AntimatterCanister_AA", MissingAntimatter);
                }
            }
        }

        public void PlaceOutput(int count)
        {
            if (count <= 0)
                return;

            Thing thing = ThingMaker.MakeThing(AADefOf.AntimatterComposite_AA);
            thing.stackCount = count;

            GenPlace.TryPlaceThing(thing, OutputPos, Find.CurrentMap, ThingPlaceMode.Near);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref CurrentAntimatterCount, "currentAntimatter");
            Scribe_Values.Look(ref CurrentPlasteelCount, "currentPlasteel");
            Scribe_Values.Look(ref ProductionTicks, "productionTicks");
        }

        public Building_InputTray GetLeftTray()
        {
            return GetTray(new IntVec3(-2, 0, 0));
        }

        public Building_InputTray GetRightTray()
        {
            return GetTray(new IntVec3(2, 0, 0));
        }

        public bool GetShouldBeRunning()
        {
            return GetReasonNotRunning() == null;
        }

        public string GetReasonNotRunning()
        {
            if (!PowerTraderComp.PowerOn)
                return "AA.NotEnoughPower".Translate();

            if (MissingPlasteel > 0)
                return "AA.MissingPlasteel".Translate(MissingPlasteel);

            if (MissingAntimatter > 0)
                return "AA.MissingAntimatter".Translate(MissingAntimatter);

            return null;
        }

        public override string GetInspectString()
        {
            string reasonNotRunning = GetReasonNotRunning();
            string hours = $"{(TicksToProduceOutput - ProductionTicks) / 2500f:F1}";
            return base.GetInspectString() + $"\n{(reasonNotRunning == null ? "AA.RunningInfo".Translate(hours) : "AA.NotRunningInfo".Translate(reasonNotRunning))}\n{"AA.Plasteel".Translate().CapitalizeFirst()}: {CurrentPlasteelCount}/{MaxPlasteel}\n{"AA.Antimatter".Translate().CapitalizeFirst()}: {CurrentAntimatterCount}/{MaxAntimatter}";
        }
    }
}
