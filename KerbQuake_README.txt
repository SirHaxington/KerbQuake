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