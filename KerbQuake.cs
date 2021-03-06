﻿using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;

// -------------------------------------------------------------------------------------------------------
// Copyright (c) 2014, Jesse Snyder (Sir Haxington)
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, 
// are permitted provided that the following conditions are met:
//
// Redistributions of source code must retain the above copyright notice, 
// this list of conditions and the following disclaimer.
//
// Redistributions in binary form must reproduce the above copyright notice,
// this list of conditions and the following disclaimer in the documentation 
// and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS 
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE ;
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY 
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
// EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// -------------------------------------------------------------------------------------------------------

// #######################################################################################################
// KERBQUAKE 1.2
// 
// KerbQuake adds camera shake for various events while in IVA. The following events will shake
// the cam as described below. The largest shakes (if many happen at once) will take precedence.
//
// Engine Shakes:
//    - The combined engines total thrust will shake the cam accordingly. Throttle up the mainsails!
//    - Intake air engines will barely shake at all.
//    - SRBs shake 2.5x more than their liquid fuel bretheren.
// 
// Atmospheric Shakes:
//    - The more dense and faster though atmospheres you move will shake harder.
//    - Hitting terminal velocity will exaggerate the shake.
//    - Re-entry fx will REALLY exaggerate the shake.
// 
// Landing & Collision Shakes: 
//    - Landing shake will depend on how hard you land and fires when each part touches the ground / water.
//    - Nearby collisions (parts popping off you ship on rough landings) will shake the cam hard.
//
// Decouplers & Launch Clamps
//    - Decouplers total ejection force will shake accordingly.
//    - Launch clamps will shake when released but won't be seen on larger take-offs.
// 
// Docking
//    - The cam will shake on successful docking, giving you visual feedback for IVA docking!
//
// Parachutes
//    - Parachutes will add to the atmospheric shake while semi-deployed.
//    - Parachutes will damped the atmospheric shake while fully deployed.
//    - Chutes will give a healthy shake when they go from semi-deployed to fully deployed.
//    - RealChutes supported.
//
// Rovers
//    - Wheeled vehicles will shake while moving over terrain.
//
// EVA (FPEVA)
//    - Shakes on RCS use.
//    - Shakes on ladder grabs.
//    - Landings shake according to landing speed.
//    - Ragdolls shake harder, continue as you slide across surfaces.
//    - Shakes on low-g steps while walking / running.
//  
// #######################################################################################################

namespace KerbQuake
{
#if DEBUG

    //################################################################################################################################
    //
    // DEBUG (thanks Trigger Au!)
    //
    //################################################################################################################################

    //This will kick us into the save called default and set the first vessel active 
    //(HAX: note, this is rough with the ARM update, asteroids spawn and mess this up)
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class Debug_AutoLoadPersistentSaveOnStartup : MonoBehaviour
    {
        //use this variable for first run to avoid the issue with when this is true and multiple addons use it
        public static bool first = true;
        public void Start()
        {
            //only do it on the first entry to the menu
            if (first)
            {
                first = false;
                HighLogic.SaveFolder = "default";
                var game = GamePersistence.LoadGame("persistent", HighLogic.SaveFolder, true, false);
                if (game != null && game.flightState != null && game.compatible)
                {
                    FlightDriver.StartAndFocusVessel(game, 0);   
                }
            }
        }


    }
#endif

    //################################################################################################################################
    //
    // KERBQUAKE
    //
    //################################################################################################################################
   
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KerbQuake : MonoBehaviour
    {
        // consts
        float[] decoupleShakeTimes        = new float[] { 0.1f,  0.2f, 0.3f, 0.4f, 0.5f };
        float[] landedShakeTimes          = new float[] { 0.15f, 0.25f, 0.35f, 0.45f, 0.55f };
        float[] paraShakeTimes            = new float[] { 0.4f };
        float[] clampShakeTimes           = new float[] { 0.5f };
        float[] dockShakeTimes            = new float[] { 0.25f };
        float[] atmoBurnDownTimes         = new float[] { 1.0f };
        float[] collisionShakeTimes       = new float[] { 0.1f, 0.2f, 0.35f, 0.5f, 0.65f, 0.75f };
        float[] evaShakeTimes             = new float[] { 0.1f, 0.2f, 0.35f, 0.5f, 0.65f, 0.75f };
        float   maxEngineForce            = 3500.0f;
        float   maxLandedForce            = 15.0f;
        float   maxDecoupleForce          = 1000.0f;
        float   maxSpdDensity             = 2000.0f;
        float   maxSpdDensityEarly        = 175.0f;
        float   maxSpdRover               = 50.0f;

        // states
        float   decoupleShakeTime         = 0.0f;
        float   paraShakeTime             = 0.0f;
        float   clampShakeTime            = 0.0f;
        float   dockShakeTime             = 0.0f;
        float   decoupleShakeForce        = 0.0f;
        float   ejectionForceTotal        = 0;
        float   engineThrustTotal         = 0;
        int     totalUndeployedChutes     = 0;
        int     totalClampedClamps        = 0;
        float   landedShakeTime           = 0.0f;
        float   landedShakeForce          = 0.0f;
        float   landedPrevSrfSpd          = 0.0f;
        int     landedPrevParts           = 0;
        double  collisionClosest          = -1.0f;
        float   collisionShakeTime        = 0.0f;
        float   evaShakeTime              = 0.0f;
        int     evaAnimShakeAmount        = 55000;
        int     evaRCSShakeAmount         = 40000;
        float   evaSrfSped                = 0;
        float   evaSrfSpedPrev            = 0;
        float   burnDownTime              = 0.0f;

        bool    doDecoupleShake           = false;
        bool    doEngineShake             = false;
        bool    doLanded                  = false;
        bool    doParaFull                = false;
        bool    doClamp                   = false;
        bool    doRover                   = false;
        bool    doEVA                     = false;

        Vector3         shakeAmt          = new Vector3(0, 0, 0);
        Quaternion      shakeRot          = new Quaternion(0, 0, 0, 0);
        AerodynamicsFX  afx;
        bool            gamePaused        = false;
        string          evaAnimState      = "";
        double          evaFuel           = 5.0;        
        
        
        // Set up callbacks / events
        public void Awake()
        {
            GameEvents.onCollision.Add(this.onVesselCollision);
            GameEvents.onStageSeparation.Add(this.onVesselStageSeparate);
            GameEvents.onPartCouple.Add(this.onVesselDock);
            GameEvents.onCrash.Add(this.onVesselCollision);
            GameEvents.onCrashSplashdown.Add(this.onVesselCollision);

            GameEvents.onGamePause.Add(this.onGamePause);
            GameEvents.onGameUnpause.Add(this.onGameUnpause);
            //Debug.Log("listening for crashes and collisions");
        }

        // This should be re-done, but works for now.
        public void onVesselCollision(EventReport report)
        {
            //Debug.Log("handling collision");
            // get distance between crashed part and vessel
            double dist = Vector3d.Distance(report.origin.transform.localPosition, FlightGlobals.ActiveVessel.transform.localPosition);

            // this next bit is for finding how close we are to the ground
            double realTerrainAlt = FlightGlobals.ActiveVessel.terrainAltitude;
            double alt = FlightGlobals.ActiveVessel.altitude;

            if (realTerrainAlt < 0)
                realTerrainAlt = 0;

            // if part is closer, do longer shake and reset timer, dont do it outside of 30 (arbitrary)
            if ((collisionClosest > dist) || (collisionClosest < 0) && (dist < 30) && (alt - realTerrainAlt) < 30)
            {
                collisionClosest = dist;
                
                if (dist <= 0)
                    collisionShakeTime = collisionShakeTimes[5];
                else if (dist <= 1)
                    collisionShakeTime = collisionShakeTimes[4];
                else if (dist <= 3)
                    collisionShakeTime = collisionShakeTimes[3];
                else if (dist <= 8)
                    collisionShakeTime = collisionShakeTimes[2];
                else if (dist <= 16)
                    collisionShakeTime = collisionShakeTimes[1];
                else
                    collisionShakeTime = collisionShakeTimes[0];
            }
        }

        // Used to find when decouplers fire
        public void onVesselStageSeparate(EventReport report)
        {
            //Debug.Log("handling separation");

            Part part = report.origin;

            if (part.vessel.vesselType == VesselType.Debris)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module.moduleName.Contains("ModuleDecouple"))
                    {
                        ModuleDecouple md = module as ModuleDecouple;
                        ejectionForceTotal += md.ejectionForce;
                    }
                    else if (module.moduleName.Contains("ModuleAnchoredDecoupler"))
                    {
                        ModuleAnchoredDecoupler md = module as ModuleAnchoredDecoupler;
                        ejectionForceTotal += md.ejectionForce;
                    }

                    ejectionForceTotal = Mathf.Clamp(ejectionForceTotal, 0, maxDecoupleForce);
                }
            }
        }

        // Used to find when a vessel docks, we'll assume you're always in control of the docking vessel and want
        // the event to fire
        public void onVesselDock(GameEvents.FromToAction<Part, Part> action)
        {
            //Debug.Log("handling dock");
            dockShakeTime = dockShakeTimes[0];
        }

        public void onGamePause()
        {
            gamePaused = true;
        }

        public void onGameUnpause()
        {
            gamePaused = false;
        }

        // find the more important rotation
        public Quaternion ReturnLargerRot(Quaternion newRot, Quaternion currentRot)
        {
            if (Quaternion.Dot(currentRot, shakeRot) > Quaternion.Dot(newRot, shakeRot))
                return currentRot;
            else
                return newRot;
        }

        // find the more important shake amount
        public Vector3 ReturnLargerAmt(Vector3 newAmt, Vector3 currentAmt)
        {
            if (Vector3.Dot(currentAmt, shakeAmt) > Vector3.Dot(newAmt, shakeAmt))
                return currentAmt;
            else
                return newAmt;
        }

        // every frame... 
        float logSt = 1.0f;
        public void Update()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;             // easier to use vessel

            // safety check
            if (vessel == null || !HighLogic.LoadedSceneIsFlight)
                return;
            
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // atmospheric shake
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // up the shake based on atmopshereic density and surface speed
            float spdDensity = (float)(vessel.atmDensity) * (float)FlightGlobals.ship_srfSpeed;

            // limit here, mainly for space planes
            spdDensity = Mathf.Clamp(spdDensity, 0, maxSpdDensityEarly);

            // exagerate shake if semideployed, dampen if deployed
            foreach (Part part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module.moduleName.Contains("ModuleParachute"))
                    {
                        ModuleParachute p = module as ModuleParachute;

                        if (p.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED && !vessel.LandedOrSplashed)
                            spdDensity *= 1.25f;

                        if (p.deploymentState == ModuleParachute.deploymentStates.DEPLOYED && !vessel.LandedOrSplashed)
                            spdDensity *= 0.75f;
                    }
                    // RealChute Support
                    if (module.moduleName.Contains("RealChuteModule"))
                    {
                        PartModule p = part.Modules["RealChuteModule"];
                        Type pType = p.GetType();
                        string depState    = (string)pType.GetField("depState").GetValue(p);
                        string secDepState = (string)pType.GetField("secDepState").GetValue(p);

                        if (depState == "PREDEPLOYED" || secDepState == "PREDEPLOYED")
                            spdDensity *= 1.25f;

                        if (depState == "DEPLOYED" || secDepState == "DEPLOYED")
                            spdDensity *= 0.75f;
                    }
                }
            }

            // lifted from DRE (thanks r4m0n), gets the mach / reentry fx
            if (afx == null)
            {
                GameObject fx = GameObject.Find("FXLogic");
                if (fx != null)
                {
                    afx = fx.GetComponent<AerodynamicsFX>();
                }
            }

            // sirhaxington special: use weird values I found to determine if mach or reentry, there has to be a better way...
            if ((afx != null) && (afx.FxScalar > 0.01))
            {
                // hack, whatever the .b color value is, always is this for re-entry, .11 something
                if (afx.fxLight.color.b < 0.12f)
                {
                    burnDownTime = atmoBurnDownTimes[0];
                }

                // hack, whatever the .b color value is, always is this for mach fx, .21 something
                if (afx.fxLight.color.b > 0.20f)
                {
                    // since we * 10, only do this if it's going to increase...
                    if (afx.FxScalar > 0.1)
                        spdDensity *= (afx.FxScalar * 10);
                }
            }

            // ease back into normal atmophere from re-entry
            if (burnDownTime > 0)
            {
                spdDensity *= (afx.FxScalar * burnDownTime * 1000);
                burnDownTime -= Time.deltaTime;
            }

            // dont go too crazy...
            spdDensity = Mathf.Clamp(spdDensity, 0, maxSpdDensity);

            if (!vessel.isEVA)
            {
                shakeAmt = ReturnLargerAmt((UnityEngine.Random.insideUnitSphere * spdDensity) / 500000, shakeAmt);
                shakeRot = ReturnLargerRot(Quaternion.Euler(0, 0, (UnityEngine.Random.Range(-0.1f, 0.1f) * spdDensity) / 5000), shakeRot);
            }

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // parachute open shake
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            int chuteCheck = 0;                                 // re-do this every frame, check against previous frame to see if chute opened

            // a chute has popped...
            // Note: Don't need to do this with realChutes as they tend to open slow anyway
            foreach (Part part in vessel.parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module.moduleName.Contains("ModuleParachute"))
                    {
                        ModuleParachute p = module as ModuleParachute;

                        if (p.deploymentState != ModuleParachute.deploymentStates.DEPLOYED)
                            chuteCheck++;
                    }
                }
            }

            // check against previous frames' chutes, then prep shake event
            if (chuteCheck < totalUndeployedChutes)
            {
                doParaFull = true;
                paraShakeTime = paraShakeTimes[0];
            }

            // set this at end of check for next frame
            totalUndeployedChutes = chuteCheck;

            // do the parachute pop shake
            if (paraShakeTime > 0 && doParaFull)
            {
                shakeAmt = ReturnLargerAmt(UnityEngine.Random.insideUnitSphere / 500, shakeAmt);
                shakeRot = ReturnLargerRot(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-0.5f, 0.5f)), shakeRot);
                paraShakeTime -= Time.deltaTime;
            }
            else if (paraShakeTime <= 0)
                doParaFull = false;

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // decoupler shakes
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
             
            // total is grabbed from event handler
            if (ejectionForceTotal > 0)
            {
                // playing around with set shake timers, not sure if I like this way but it works for now
                if (ejectionForceTotal <= 15)
                    decoupleShakeTime = decoupleShakeTimes[0];
                else if (ejectionForceTotal <= 250)
                    decoupleShakeTime = decoupleShakeTimes[1];
                else if (ejectionForceTotal <= 500)
                    decoupleShakeTime = decoupleShakeTimes[2];
                else if (ejectionForceTotal <= 1000)
                    decoupleShakeTime = decoupleShakeTimes[3];
                else
                    decoupleShakeTime = decoupleShakeTimes[4];

                decoupleShakeForce = ejectionForceTotal;

                doDecoupleShake = true;
                ejectionForceTotal = 0;
            }

            // do the decoupler shake
            if (decoupleShakeTime > 0 && doDecoupleShake)
            {
                shakeAmt = ReturnLargerAmt(UnityEngine.Random.insideUnitSphere * decoupleShakeForce / 500000, shakeAmt);
                shakeRot = ReturnLargerRot(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-0.5f, 0.5f)), shakeRot);
                decoupleShakeTime -= Time.deltaTime;
            }
            else if (decoupleShakeTime <= 0)
            {
                doDecoupleShake = false;
            }

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // docking shakes
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // do the dock shake...we set the time from the handler, no need for other checks
            if (dockShakeTime > 0)
            {
                shakeAmt = ReturnLargerAmt(UnityEngine.Random.insideUnitSphere / 1000, shakeAmt);
                shakeRot = ReturnLargerRot(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-0.07f, 0.07f)), shakeRot);
                dockShakeTime -= Time.deltaTime;
            }

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // launch clamp shakes
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            int clampCheck = 0;                                 

            // a clamp has detached...
            foreach (Part part in vessel.parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module.moduleName.Contains("LaunchClamp"))
                    {
                        LaunchClamp lc = module as LaunchClamp;

                        if (lc.enabled)
                            clampCheck++;
                    }
                }
            }

            // check against previous frames' chutes, then prep shake event
            if (clampCheck < totalClampedClamps)
            {
                doClamp = true;
                clampShakeTime = clampShakeTimes[0];
            }

            // set this at end of check for next frame
            totalClampedClamps = clampCheck;

            // do the parachute pop shake
            if (clampShakeTime > 0 && doClamp)
            {
                shakeAmt = ReturnLargerAmt(UnityEngine.Random.insideUnitSphere / 500, shakeAmt);
                shakeRot = ReturnLargerRot(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-0.7f, 0.7f)), shakeRot);
                clampShakeTime -= Time.deltaTime;
            }
            else if (clampShakeTime <= 0)
                doClamp = false;

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // engine shakes
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // check both engine types (rapier uses ModuleEnginesFX, most use ModuleEngines) then base shake on thrust amount
            foreach (Part part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module.moduleName.Contains("ModuleEnginesFX"))
                    {
                        ModuleEnginesFX e = module as ModuleEnginesFX;

                        if (e.isOperational)
                        {
                            float solidScalar = 1.0f;            // scale up SRBs

                            if (e.propellants.Count > 0)
                            {
                                foreach (Propellant p in e.propellants)
                                {
                                    if (p.name == "SolidFuel")
                                    {
                                        solidScalar = 2.5f;
                                    }
                                }
                            }
                            engineThrustTotal += (e.finalThrust * solidScalar);
                        }
                         
                        if (engineThrustTotal > 0)
                            doEngineShake = true;
                    }
                    else if (module.moduleName.Contains("ModuleEngines"))
                    {
                        ModuleEngines e = module as ModuleEngines;

                        if (e.isOperational)
                        {
                            float typeScalar = 1.0f;            // scale up SRBs, down jets

                            if (e.propellants.Count > 0)
                            {
                                foreach (Propellant p in e.propellants)
                                {
                                    if (p.name == "SolidFuel")
                                    {
                                        typeScalar = 2.5f;
                                    }
                                    else if (p.name == "IntakeAir")
                                    {
                                        typeScalar = 0.01f;
                                    }
                                }
                            }
                            engineThrustTotal += (e.finalThrust * typeScalar);
                        }

                        if (engineThrustTotal > 0)
                            doEngineShake = true;
                    }

                    // don't go too crazy...
                    engineThrustTotal = Mathf.Clamp(engineThrustTotal, 0, maxEngineForce);
                }
            }

            // do engine shake...
            if (engineThrustTotal > 0 && doEngineShake)
            {
                shakeAmt = ReturnLargerAmt((UnityEngine.Random.insideUnitSphere * (engineThrustTotal / 1000)) / 800, shakeAmt);
                shakeRot = ReturnLargerRot(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-0.8f, 0.8f) * (engineThrustTotal / 1000)), shakeRot);
            }
            else if (engineThrustTotal <= 0)
            {
                doEngineShake = false;            
            }

            // reset every frame
            engineThrustTotal = 0;

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // nearby collision shakes
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // do the collision shake...we set the time from the handler, no need for other checks
            if (collisionShakeTime > 0)
            {
                shakeAmt = ReturnLargerAmt(UnityEngine.Random.insideUnitSphere / 50, shakeAmt);
                shakeRot = ReturnLargerRot(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-1.5f, 1.5f)), shakeRot);
                collisionShakeTime -= Time.deltaTime;
            }

            // reset for next frame, use negative since we're looking for distance now
            collisionClosest = -1;

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // rover ground shakes
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            float spdRover = (float)FlightGlobals.ship_srfSpeed;

            doRover = false;
            float roverScalar = 1.0f;
            foreach (Part part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module.moduleName.Contains("ModuleWheel"))
                    {
                        if (part.GroundContact)
                        {
                            if (vessel.landedAt.Length == 0 || vessel.landedAt.ToString() == "KSC")
                            {
                                roverScalar = 2.0f;
                            }
                            // maybe later, do biome specific shakes
                            //CBAttributeMap currentBiome = vessel.mainBody.BiomeMap;
                            //print(currentBiome.GetAtt(vessel.latitude * Mathf.Deg2Rad, vessel.longitude * Mathf.Deg2Rad).name);
                            //print(currentBiome.ToString());
                            
                            doRover = true;
                        }
                    }
                    if (module.moduleName.Contains("ModuleLandingGear"))
                    {
                        if (part.Landed)
                        {
                            if (vessel.landedAt.Length == 0 || vessel.landedAt.ToString() == "KSC")         // basically, in and around KSC
                            {
                                roverScalar = 2.0f;
                            }
                        
                            doRover = true;
                        }
                    }
                }
            }

            spdRover *= roverScalar;

            // dont go too crazy...
            spdRover = Mathf.Clamp(spdRover, 0, maxSpdRover);

            if (doRover && !vessel.isEVA)
            {
                shakeAmt = ReturnLargerAmt((UnityEngine.Random.insideUnitSphere * spdRover) / 50000, shakeAmt);
                shakeRot = ReturnLargerRot(Quaternion.Euler(0, 0, (UnityEngine.Random.Range(-0.1f, 0.1f) * spdRover) / 500), shakeRot);
            }

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // landing shakes
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // note, parts that break off may throw this off
            int landedCurParts = 0;
            foreach (Part part in vessel.Parts)
            {
                if (part.GroundContact || part.WaterContact)
                    landedCurParts++;
            }

            // if more parts are touching the ground this frame...
            if (landedCurParts > landedPrevParts)
            {
                doLanded = true;

                // do I need horizontal surface speed as well? hmmm...
                landedShakeForce = landedPrevSrfSpd;

                if (landedShakeForce <= 0.5)
                    landedShakeTime = landedShakeTimes[0];
                else if (landedShakeForce <= 1.5)
                    landedShakeTime = landedShakeTimes[1];
                else if (landedShakeForce <= 3.0)
                    landedShakeTime = landedShakeTimes[2];
                else if (landedShakeForce <= 5.0)
                    landedShakeTime = landedShakeTimes[3];
                else
                    landedShakeTime = landedShakeTimes[4];

                if (doRover)
                    landedShakeForce /= 2;

                landedShakeForce = Mathf.Clamp(landedShakeForce, 0, maxLandedForce);
            }

            // set the current parts for the next frame
            landedPrevParts = landedCurParts;

            // do the landing / touching ground / water shake
            if (doLanded && !vessel.isEVA)
            {
                if (landedShakeTime > 0)
                {
                    shakeAmt = ReturnLargerAmt((UnityEngine.Random.insideUnitSphere * landedShakeForce) / 3600, shakeAmt);
                    shakeRot = ReturnLargerRot(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-0.1f, 0.1f) * landedShakeForce), shakeRot);
                    landedShakeTime -= Time.deltaTime;
                }
                else
                {
                    doLanded = false;
                }
            }

            // set the speed for the next frame
            landedPrevSrfSpd = (float)FlightGlobals.ActiveVessel.srfSpeed;

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // EVA Shakes (under construction)
            //
            // to do: polish shakes (check on a few planets), rotation add back in
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            if (vessel.isEVA)
            {
                KerbalEVA eva = (KerbalEVA)vessel.rootPart.Modules["KerbalEVA"];

                // if an anim has changed
                if (eva.fsm.currentStateName != evaAnimState)
                {
                    //print(evaSrfSpedPrev);
                    if (eva.fsm.currentStateName == "Landing")                              // standard landing from a jump / RCS
                    {
                        evaShakeTime = evaShakeTimes[2];
                        evaAnimShakeAmount = (int)(45000 / evaSrfSpedPrev);

                    }
                    else if (eva.fsm.currentStateName == "Ragdoll")                         // we landed too hard / jumped and landed at an odd angle
                    {
                        evaShakeTime = evaShakeTimes[5];
                        evaAnimShakeAmount = (int)(8000 / evaSrfSpedPrev);
                    }
                    else if (eva.fsm.currentStateName == "Low G Bound (Grounded - FPS)")    // each step on a low g world should be felt
                    {
                        evaShakeTime = evaShakeTimes[2];
                        evaAnimShakeAmount = (int)(50000);
                    }
                    else if (eva.fsm.currentStateName == "Ladder (Acquire)")                 // feel the grab a bit more
                    {
                        evaShakeTime = evaShakeTimes[5];
                        evaAnimShakeAmount = (int)(1000000);
                    }
                    evaAnimState = eva.fsm.currentStateName;
                    //print(evaAnimState);
                }
                else if (evaAnimState == "Ragdoll" && vessel.Landed && evaShakeTime <= 0)    // when ragging and sliding along the surface, keep shaking
                {
                    evaShakeTime = evaShakeTimes[5];
                    evaAnimShakeAmount = (int)(8000 / evaSrfSpedPrev);
                }
                else if (evaAnimState == "Ladder (Acquire)" && evaShakeTime >= 0)           // when ragging and sliding along the surface or falling, update shaking
                {
                    if (evaShakeTime < 0.3)
                        evaAnimShakeAmount = (int)(4000);
                }

                // update shake based on timers
                if (evaShakeTime > 0)
                {
                    shakeAmt = ReturnLargerAmt(UnityEngine.Random.insideUnitSphere / evaAnimShakeAmount, shakeAmt);
                    evaShakeTime -= Time.deltaTime;
                }

                // RCS Cam Shake
                if (Math.Round(eva.Fuel, 3) != Math.Round(evaFuel, 3))
                {
                    evaFuel = eva.Fuel;
                    shakeAmt = ReturnLargerAmt(UnityEngine.Random.insideUnitSphere / evaRCSShakeAmount, shakeAmt);
                    Math.Round(eva.Fuel, 3);
                }

                // grab frame two behind to test against
                evaSrfSpedPrev = evaSrfSped;
                evaSrfSped     = (float)vessel.srfSpeed;                
            }

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //
            // DO THE HARLEMSHAKE! o/\o
            //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // hopefully we've picked the largest values... also, don't shake while paused, looks dumb
            if (InternalCamera.Instance != null)
            {
                if (!gamePaused && InternalCamera.Instance.isActive)
                {
                   InternalCamera.Instance.camera.transform.localPosition = shakeAmt;
                   InternalCamera.Instance.camera.transform.localRotation *= shakeRot;
                }
            }


            // for a different first person EVA mod...
            bool FPEVAMod = false;
            foreach (Part part in vessel.Parts)
                foreach (PartModule module in part.Modules)
                    if (module.moduleName.Contains("EVACamera"))
                        FPEVAMod = true;

            // rotation is wonky in EVA, skip
            if (vessel.isEVA && FlightCamera.fetch.minDistance == 0.01f && FlightCamera.fetch.maxDistance == 0.01f)
            {
                if (shakeAmt.x != 0 && shakeAmt.y != 0 && shakeAmt.z != 0)
                    FlightCamera.fetch.transform.localPosition += shakeAmt;
            }
            else if (vessel.isEVA && FPEVAMod)
            {
                FlightCamera.fetch.transform.localPosition += (shakeAmt * 1000);
            }

            // reset the shake vals every frame and start over...
            shakeAmt = new Vector3(0.0f, 0.0f, 0.0f);
            shakeRot = new Quaternion(0.0f, 0.0f, 0.0f, 0.0f);
        }
    }
}


