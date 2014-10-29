// #######################################################################################################
// KERBQUAKE 1.2.1 (dx/dt's unofficial optimisation patch for KerbQuake v1.2)
//
// Bugfixes:
// - Shaking removed from the probe control room when using probe control room mod
// - Fixed RealChute compatibility broken with RealChute 1.2 update
//
// #######################################################################################################

// #######################################################################################################
// KERBQUAKE 1.2
// 
// - First Person EVA shake support if using the "Through the Eyes of a Kerbal" (ForceIVA) mod.
// - Overall atmospheric shake capped so space planes aren't crazy on take off.
// - Intake air based engines shake next to nothing for smoother plane rides.
// - SRBS shake 2.5x more than their liquid fuel brethren.
// - Fixes for landing gear not counting for ground based shakes.
// - Smoother transitions out of re-entry shake.
//
// #######################################################################################################

// #######################################################################################################
// KERBQUAKE 1.1
// 
// - Rover / wheeled vehicle shake added.
// - RealChute support added (note RealChutes tend to open slowly so no "hard open" shake like in stock).
// - Fixed bug on terminal velocity where it might reduce shake at the low end.
// - Switched randoms to be UnityEngine specfic.
//
// #######################################################################################################

// #######################################################################################################
// KERBQUAKE 1.0
// 
// KerbQuake adds camera shake for various events while in IVA. The following events will shake
// the cam as described below. The largest shakes (if many happen at once) will take precedence.
//
// Engine Shakes:
//    - The combined engines total thrust will shake the cam accordingly. Throttle up the mainsails!
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
//  
// #######################################################################################################