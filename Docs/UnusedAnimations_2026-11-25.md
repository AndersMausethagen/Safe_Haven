# Unused animations (2026-11-25)

Clips nothing in the built game plays: no build scene, Resources asset, or anything they reach (prefabs, Animator Controllers, override controllers, blend trees, ScriptableObjects) points at them. Measured clip by clip, also inside model files that hold many clips. A clip in a controller state that no code ever triggers still counts as used here. Editor tools and the packs' own demo scenes don't count.

**619 of 1348 clips are unused.**

| Pack / folder | Unused | Used |
|---|---:|---:|
| Malbers Animations | 270 | 95 |
| Kevin Iglesias | 191 | 105 |
| Quaternius/Universal Animation Library | 77 | 9 |
| Quaternius/Universal Animation Library 2 | 21 | 23 |
| _Project/Art/Enemies | 21 | 25 |
| _Project/Art/Wildlife | 19 | 19 |
| StylizedCharacter/Animations | 9 | 444 |
| StylizedCharacter/Meshes | 6 | 0 |
| Starter Runtime/ThirdPersonController | 1 | 8 |
| Starter Runtime/SpaceRobotKyle | 1 | 0 |
| VIVID_Arts/Shared | 1 | 0 |
| _Project/Art/Animations | 1 | 0 |
| Unity Timeline (package sample) | 1 | 0 |

Also unused: the attack asset `Data/Combat/Attacks/GreataxeSweep.asset` (no weapon or monster refers to it).

## Every unused clip, by folder

### Kevin Iglesias/Human Animations/Animations/Female/Combat (3)

- HumanF@CombatDamage01 (1.00 s)
- HumanF@CombatIdle01 (1.33 s)
- HumanF@Death01 (0.73 s)

### Kevin Iglesias/Human Animations/Animations/Female/Combat/1H (3)

- HumanF@Attack1H01_L (1.10 s)
- HumanF@Attack1H01_R (1.10 s)
- HumanF@CombatIdle1H01 (1.33 s)

### Kevin Iglesias/Human Animations/Animations/Female/Combat/2H (2)

- HumanF@Attack2H01 (1.60 s)
- HumanF@CombatIdle2H01 (1.33 s)

### Kevin Iglesias/Human Animations/Animations/Female/Combat/Polearm (2)

- HumanF@AttackPolearm01 (1.37 s)
- HumanF@CombatIdlePolearm01 (1.33 s)

### Kevin Iglesias/Human Animations/Animations/Female/Combat/Shield (1)

- HumanF@AttackShield01 (0.90 s)

### Kevin Iglesias/Human Animations/Animations/Female/Idles (4)

- HumanF@Idle01 (2.70 s)
- HumanF@Idle01-Idle02 (0.67 s)
- HumanF@Idle02 (2.70 s)
- HumanF@Idle02-Idle01 (0.77 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Jump (7)

- HumanF@Fall01 (1.00 s)
- HumanF@Jump01 (1.53 s)
- HumanF@Jump01 - Begin (0.67 s)
- HumanF@Jump01 - Land (0.60 s)
- HumanF@Jump01 [RM] (1.53 s)
- HumanF@Jump01 [RM] - Begin (0.67 s)
- HumanF@Jump01 [RM] - Land (0.60 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Run (8)

- HumanF@Run01_Backward (0.60 s)
- HumanF@Run01_BackwardLeft (0.60 s)
- HumanF@Run01_BackwardRight (0.60 s)
- HumanF@Run01_Forward (0.60 s)
- HumanF@Run01_ForwardLeft (0.60 s)
- HumanF@Run01_ForwardRight (0.60 s)
- HumanF@Run01_Left (0.60 s)
- HumanF@Run01_Right (0.60 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/RootMotion (8)

- HumanF@Run01_Backward [RM] (0.60 s)
- HumanF@Run01_BackwardLeft [RM] (0.60 s)
- HumanF@Run01_BackwardRight [RM] (0.60 s)
- HumanF@Run01_Forward [RM] (0.60 s)
- HumanF@Run01_ForwardLeft [RM] (0.60 s)
- HumanF@Run01_ForwardRight [RM] (0.60 s)
- HumanF@Run01_Left [RM] (0.60 s)
- HumanF@Run01_Right [RM] (0.60 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Sprint (5)

- HumanF@Sprint01_Forward (0.53 s)
- HumanF@Sprint01_ForwardLeft (0.53 s)
- HumanF@Sprint01_ForwardRight (0.53 s)
- HumanF@Sprint01_Left (0.53 s)
- HumanF@Sprint01_Right (0.53 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Sprint/RootMotion (5)

- HumanF@Sprint01_Forward [RM] (0.53 s)
- HumanF@Sprint01_ForwardLeft [RM] (0.53 s)
- HumanF@Sprint01_ForwardRight [RM] (0.53 s)
- HumanF@Sprint01_Left [RM] (0.53 s)
- HumanF@Sprint01_Right [RM] (0.53 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Strafe/StrafeRun (6)

- HumanF@StrafeRun01_BackwardLeft (0.60 s)
- HumanF@StrafeRun01_BackwardRight (0.60 s)
- HumanF@StrafeRun01_ForwardLeft (0.60 s)
- HumanF@StrafeRun01_ForwardRight (0.60 s)
- HumanF@StrafeRun01_Left (0.60 s)
- HumanF@StrafeRun01_Right (0.60 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Strafe/StrafeRun/RootMotion (6)

- HumanF@StrafeRun01_BackwardLeft [RM] (0.60 s)
- HumanF@StrafeRun01_BackwardRight [RM] (0.60 s)
- HumanF@StrafeRun01_ForwardLeft [RM] (0.60 s)
- HumanF@StrafeRun01_ForwardRight [RM] (0.60 s)
- HumanF@StrafeRun01_Left [RM] (0.60 s)
- HumanF@StrafeRun01_Right [RM] (0.60 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Turn (4)

- HumanF@Turn01_Left (1.00 s)
- HumanF@Turn01_Left [RM] (1.00 s)
- HumanF@Turn01_Right (1.00 s)
- HumanF@Turn01_Right [RM] (1.00 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Walk (8)

- HumanF@Walk01_Backward (0.80 s)
- HumanF@Walk01_BackwardLeft (0.80 s)
- HumanF@Walk01_BackwardRight (0.80 s)
- HumanF@Walk01_Forward (0.80 s)
- HumanF@Walk01_ForwardLeft (0.80 s)
- HumanF@Walk01_ForwardRight (0.80 s)
- HumanF@Walk01_Left (0.80 s)
- HumanF@Walk01_Right (0.80 s)

### Kevin Iglesias/Human Animations/Animations/Female/Movement/Walk/RootMotion (8)

- HumanF@Walk01_Backward [RM] (0.80 s)
- HumanF@Walk01_BackwardLeft [RM] (0.80 s)
- HumanF@Walk01_BackwardRight [RM] (0.80 s)
- HumanF@Walk01_Forward [RM] (0.80 s)
- HumanF@Walk01_ForwardLeft [RM] (0.80 s)
- HumanF@Walk01_ForwardRight [RM] (0.80 s)
- HumanF@Walk01_Left [RM] (0.80 s)
- HumanF@Walk01_Right [RM] (0.80 s)

### Kevin Iglesias/Human Animations/Animations/Female/Social/Conversation (1)

- HumanF@Talk01 (2.27 s)

### Kevin Iglesias/Human Animations/Animations/Female/Work/Farming (3)

- HumanF@FarmingWithPlow01_R - Begin (0.67 s)
- HumanF@FarmingWithPlow01_R - Loop (1.57 s)
- HumanF@FarmingWithPlow01_R - Stop (0.67 s)

### Kevin Iglesias/Human Animations/Animations/Female/Work/Fishing (9)

- HumanF@Fishing01 - Begin (0.50 s)
- HumanF@Fishing01 - Loop (4.00 s)
- HumanF@Fishing01 - Stop (0.50 s)
- HumanF@FishingFighting01 (2.07 s)
- HumanF@FishingPullOut01 (1.57 s)
- HumanF@FishingPullOut02 - Begin (1.57 s)
- HumanF@FishingPullOut02 - Loop (2.00 s)
- HumanF@FishingPullOut02 - Stop (0.60 s)
- HumanF@FishingThrow01 (1.30 s)

### Kevin Iglesias/Human Animations/Animations/Female/Work/Gathering (3)

- HumanF@Gathering01 (1.00 s)
- HumanF@Gathering02 (1.00 s)
- HumanF@Gathering03 (1.00 s)

### Kevin Iglesias/Human Animations/Animations/Female/Work/Hammering (6)

- HumanF@HammeringGround01_L - Begin (1.07 s)
- HumanF@HammeringGround01_L - Loop (1.00 s)
- HumanF@HammeringGround01_L - Stop (1.13 s)
- HumanF@HammeringGround01_R - Begin (1.07 s)
- HumanF@HammeringGround01_R - Loop (1.00 s)
- HumanF@HammeringGround01_R - Stop (1.13 s)

### Kevin Iglesias/Human Animations/Animations/Female/Work/Mining (4)

- HumanF@MiningOneHand01_L - Ground (0.97 s)
- HumanF@MiningOneHand01_L - Wall (0.97 s)
- HumanF@MiningOneHand01_R - Ground (0.97 s)
- HumanF@MiningOneHand01_R - Wall (0.97 s)

### Kevin Iglesias/Human Animations/Animations/Male/Combat (6)

- HumanM@CombatDeath01 (1.50 s)
- HumanM@CombatDeath02 (1.10 s)
- HumanM@CombatDeath03 (1.17 s)
- HumanM@CombatDeath04 (1.37 s)
- HumanM@Death02 (0.67 s)
- HumanM@Dodge01 (1.07 s)

### Kevin Iglesias/Human Animations/Animations/Male/Combat/1H (6)

- HumanM@Attack1H02_L (1.03 s)
- HumanM@Attack1H03_L (1.13 s)
- HumanM@Attack1H04_L (1.17 s)
- HumanM@Attack1H05_L (1.17 s)
- HumanM@AttackDW02 (1.23 s)
- HumanM@CombatExit1H01 (0.67 s)

### Kevin Iglesias/Human Animations/Animations/Male/Combat/2H (2)

- HumanM@CombatEnter2H01 (0.53 s)
- HumanM@CombatExit2H01 (0.67 s)

### Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow (1)

- HumanM@BowIdle02 (1.33 s)

### Kevin Iglesias/Human Animations/Animations/Male/Combat/Polearm (2)

- HumanM@CombatEnterPolearm01 (0.53 s)
- HumanM@CombatExitPolearm01 (0.67 s)

### Kevin Iglesias/Human Animations/Animations/Male/Combat/Shield (1)

- HumanM@BlockShield01 - Loop (1.33 s)

### Kevin Iglesias/Human Animations/Animations/Male/Combat/Unarmed (4)

- HumanM@AttackKick01_L (1.00 s)
- HumanM@AttackKick02_L (0.80 s)
- HumanM@AttackPunch02_L (0.47 s)
- HumanM@AttackPunch03_L (0.90 s)

### Kevin Iglesias/Human Animations/Animations/Male/Idles (4)

- HumanM@Idle01 (2.70 s)
- HumanM@Idle01-Idle02 (0.67 s)
- HumanM@Idle02 (2.70 s)
- HumanM@Idle02-Idle01 (0.77 s)

### Kevin Iglesias/Human Animations/Animations/Male/Misc/Unsheathe (8)

- HumanM@SheatheBack01_Both (0.73 s)
- HumanM@SheatheBack01_L (0.73 s)
- HumanM@SheatheHips01_Both (0.73 s)
- HumanM@SheatheHips01_L (0.73 s)
- HumanM@UnsheatheBack01_Both (0.73 s)
- HumanM@UnsheatheBack01_L (0.73 s)
- HumanM@UnsheatheHips01_Both (0.73 s)
- HumanM@UnsheatheHips01_L (0.73 s)

### Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump (4)

- HumanM@Jump01 (1.53 s)
- HumanM@Jump01 [RM] (1.53 s)
- HumanM@Jump01 [RM] - Begin (0.67 s)
- HumanM@Jump01 [RM] - Land (0.67 s)

### Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/RootMotion (8)

- HumanM@Run01_Backward [RM] (0.60 s)
- HumanM@Run01_BackwardLeft [RM] (0.60 s)
- HumanM@Run01_BackwardRight [RM] (0.60 s)
- HumanM@Run01_Forward [RM] (0.60 s)
- HumanM@Run01_ForwardLeft [RM] (0.60 s)
- HumanM@Run01_ForwardRight [RM] (0.60 s)
- HumanM@Run01_Left [RM] (0.60 s)
- HumanM@Run01_Right [RM] (0.60 s)

### Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint (5)

- HumanM@Sprint01_Forward (0.53 s)
- HumanM@Sprint01_ForwardLeft (0.53 s)
- HumanM@Sprint01_ForwardRight (0.53 s)
- HumanM@Sprint01_Left (0.53 s)
- HumanM@Sprint01_Right (0.53 s)

### Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/RootMotion (5)

- HumanM@Sprint01_Forward [RM] (0.53 s)
- HumanM@Sprint01_ForwardLeft [RM] (0.53 s)
- HumanM@Sprint01_ForwardRight [RM] (0.53 s)
- HumanM@Sprint01_Left [RM] (0.53 s)
- HumanM@Sprint01_Right [RM] (0.53 s)

### Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun (6)

- HumanM@StrafeRun01_BackwardLeft (0.60 s)
- HumanM@StrafeRun01_BackwardRight (0.60 s)
- HumanM@StrafeRun01_ForwardLeft (0.60 s)
- HumanM@StrafeRun01_ForwardRight (0.60 s)
- HumanM@StrafeRun01_Left (0.60 s)
- HumanM@StrafeRun01_Right (0.60 s)

### Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/RootMotion (6)

- HumanM@StrafeRun01_BackwardLeft [RM] (0.60 s)
- HumanM@StrafeRun01_BackwardRight [RM] (0.60 s)
- HumanM@StrafeRun01_ForwardLeft [RM] (0.60 s)
- HumanM@StrafeRun01_ForwardRight [RM] (0.60 s)
- HumanM@StrafeRun01_Left [RM] (0.60 s)
- HumanM@StrafeRun01_Right [RM] (0.60 s)

### Kevin Iglesias/Human Animations/Animations/Male/Movement/Turn (2)

- HumanM@Turn01_Left [RM] (1.00 s)
- HumanM@Turn01_Right [RM] (1.00 s)

### Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/RootMotion (8)

- HumanM@Walk01_Backward [RM] (0.80 s)
- HumanM@Walk01_BackwardLeft [RM] (0.80 s)
- HumanM@Walk01_BackwardRight [RM] (0.80 s)
- HumanM@Walk01_Forward [RM] (0.80 s)
- HumanM@Walk01_ForwardLeft [RM] (0.80 s)
- HumanM@Walk01_ForwardRight [RM] (0.80 s)
- HumanM@Walk01_Left [RM] (0.80 s)
- HumanM@Walk01_Right [RM] (0.80 s)

### Kevin Iglesias/Human Animations/Animations/Male/Social/Conversation (1)

- HumanM@Talk01 (2.27 s)

### Kevin Iglesias/Human Animations/Animations/Masked Poses (6)

- HumanF@ObjectGripShoulder01_L (0.33 s)
- HumanF@ObjectGripShoulder01_R (0.33 s)
- HumanF@ObjectGripShoulder02_L (0.33 s)
- HumanF@ObjectGripShoulder02_R (0.33 s)
- HumanF@WeaponHold2H01 (0.33 s)
- HumanF@WeaponHoldPolearm01 (0.33 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Actions (1)

- H_Drink (3.80 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Attacks (1)

- H_Attack_Back_Leg (1.00 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Damage (6)

- H_Get Hit_Back_Left Mask (1.43 s) — in `H_Get Hit_Back_Left.FBX`
- H_Get Hit_Back_Right_Mask (1.43 s) — in `H_Get Hit_Back_Right.FBX`
- H_Get Hit_Front_Left_Mask (1.73 s) — in `H_Get Hit_Front_Left.FBX`
- H_Get Hit_Front_Right_Mask (1.73 s) — in `H_Get Hit_Front_Right.FBX`
- H_Get Hit_Left_Mask (2.17 s) — in `H_Get Hit_Left.FBX`
- H_Get Hit_Right (2.17 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Fall (7)

- H_Fall_EnterWater (1.50 s)
- H_Fall_From_Edge (1.00 s)
- H_Fall_High (0.67 s)
- H_Fall_High_Forward (0.67 s)
- H_Fall_Land (0.83 s)
- H_Fall_Low (0.67 s)
- H_Fall_Low_Forward (0.67 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Fly (13)

- H_Fly_Back (1.07 s)
- H_Fly_Flap (0.80 s)
- H_Fly_Flap_L (0.80 s)
- H_Fly_Flap_R (0.80 s)
- H_Fly_Glide (1.07 s)
- H_Fly_Glide Closed (1.07 s)
- H_Fly_Glide Closed_L (1.07 s)
- H_Fly_Glide Closed_R (1.07 s)
- H_Fly_Glide_L (1.07 s)
- H_Fly_Glide_R (1.07 s)
- H_Fly_Stand (1.07 s)
- H_Fly_Stand_L (1.07 s)
- H_Fly_Stand_R (1.07 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Fly/Strafe (7)

- H_Fly_Strafe_Back_Left (1.07 s)
- H_Fly_Strafe_Back_Right (1.07 s)
- H_Fly_Strafe_Forward (1.07 s)
- H_Fly_Strafe_Forward_Left (1.07 s)
- H_Fly_Strafe_Forward_Right (1.07 s)
- H_Fly_Strafe_Left (1.07 s)
- H_Fly_Strafe_Right (1.07 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Idles (2)

- H_Idle_Decline (3.27 s) — in `H_Idle_Incline_Decline.FBX`
- H_Idle_Incline (3.30 s) — in `H_Idle_Incline_Decline.FBX`

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Jumps (6)

- H_Canter_Land (0.63 s) — in `H_Jump_CanterLand.FBX`
- H_Jump_Canter (1.57 s) — in `H_Jump_CanterLand.FBX`
- H_Jump_Canter_Land (0.57 s)
- H_Jump_Forward_Land (0.47 s)
- H_Jump_Gallop_Land (0.57 s)
- H_Jump_Sprint_Land (0.57 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Locomotion (41)

- H_Amble (1.07 s)
- H_Amble_IP (1.07 s)
- H_Canter (0.67 s)
- H_Canter (0.67 s)
- H_Canter_Left (0.67 s)
- H_Canter_Left_135 (0.67 s)
- H_Canter_Offset (0.67 s) — in `H_Canter.FBX`
- H_Canter_Right (0.67 s)
- H_Canter_Right_135 (0.67 s)
- H_Gallop (0.60 s)
- H_Gallop_Left (0.60 s)
- H_Gallop_Left (0.60 s)
- H_Gallop_Right (0.60 s)
- H_Sprint (0.53 s)
- H_Sprint_Left (0.53 s)
- H_Sprint_Right (0.53 s)
- H_Trot (0.80 s)
- H_Trot_Left (0.80 s)
- H_Trot_Left_135 (0.80 s)
- H_Trot_Pace (0.93 s)
- H_Trot_Right (0.80 s)
- H_Trot_Right_135 (0.80 s)
- H_Turn_Neigh_Left (1.40 s)
- H_Turn_Neigh_Right (1.40 s)
- H_Walk (1.07 s)
- H_Walk Left (1.07 s)
- H_Walk Right (1.07 s)
- H_Walk_Back (1.07 s)
- H_Walk_Back_Left_60 (1.07 s) — in `H_Walk_Back_Left.FBX`
- H_Walk_Back_Right_60 (1.07 s) — in `H_Walk_Back_Right.FBX`
- H_Walk_Down (1.07 s)
- H_Walk_End_Left (1.07 s) — in `H_Walk_End_L.fbx`
- H_Walk_End_Right (1.07 s) — in `H_Walk_End_R.fbx`
- H_Walk_Left_135 (1.07 s)
- H_Walk_Left_60 (1.07 s) — in `H_Walk_Left.FBX`
- H_Walk_Right_135 (1.07 s)
- H_Walk_Right_180 (2.13 s) — in `H_Walk_Left_180.FBX`
- H_Walk_Right_180 (2.13 s)
- H_Walk_Right_60 (1.07 s) — in `H_Walk_Right.FBX`
- H_Walk_Start (1.33 s)
- H_Walk_Up (1.07 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Recover (2)

- H_Recover Left (2.83 s)
- H_Recover Right (2.83 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Strafe (14)

- H_Canter_Strafe_Forward_Left (0.67 s)
- H_Canter_Strafe_Forward_Right (0.67 s)
- H_Canter_Strafe_Left (0.67 s)
- H_Canter_Strafe_Right (0.67 s)
- H_Trot_Strafe_Forward_Left (0.80 s)
- H_Trot_Strafe_Forward_Right (0.80 s)
- H_Trot_Strafe_Left (0.80 s)
- H_Trot_Strafe_Right (0.80 s)
- H_Turn_Left_Strafe (1.07 s)
- H_Turn_Right_Strafe (1.07 s)
- H_Walk_Strafe_Forward_Left (1.07 s)
- H_Walk_Strafe_Forward_Right (1.07 s)
- H_Walk_Strafe_Left (1.07 s)
- H_Walk_Strafe_Right (1.07 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/Swim (7)

- H_Swim_Back (1.07 s)
- H_Swim_Back_Left (1.07 s)
- H_Swim_Back_Right (1.07 s)
- H_Swim_Left (1.07 s)
- H_Swim_Right (1.07 s)
- H_Swim_Turn_Left_90 (1.00 s)
- H_Swim_Turn_Right_90 (1.00 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Actions (3)

- Rider_Sleep_Enter (3.00 s) — in `Rider_Sleep.FBX`
- Rider_Sleep_Exit (2.53 s) — in `Rider_Sleep.FBX`
- Rider_Sleep_Loop (5.83 s) — in `Rider_Sleep.FBX`

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Attacks (1)

- Rider_Attack_Back_Leg (0.60 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Carriage (9)

- Rider_CarriageCart_Speed1 (3.17 s)
- Rider_CarriageCart_Speed1_Left (3.17 s)
- Rider_CarriageCart_Speed1_Right (3.17 s)
- Rider_CarriageCart_Speed2 (1.60 s)
- Rider_CarriageCart_Speed2_Left (1.60 s)
- Rider_CarriageCart_Speed2_Right (1.60 s)
- Rider_CarriageCart_Wip (0.90 s)
- Rider_CarriageCart_Wip_Left (0.90 s)
- Rider_CarriageCart_Wip_Right (0.90 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Deaths (4)

- Rider_Death01_Opp (1.90 s)
- Rider_Death02 (2.67 s)
- Rider_Death02_Opp (2.67 s)
- Rider_Death_Gallop (2.40 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Fall (5)

- Rider_Fall_EnterWater (1.83 s)
- Rider_Fall_From_Edge (1.00 s)
- Rider_Fall_High (0.67 s)
- Rider_Fall_Land (0.83 s)
- Rider_Fall_Low (0.67 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Fly (6)

- Rider_Fly_Flap (0.80 s)
- Rider_Fly_Glide (1.07 s)
- Rider_Fly_Stand (1.07 s)
- Rider_Fly_Stand_L (1.07 s) — in `Rider_Fly_Stand L.fbx`
- Rider_Fly_Stand_Lean (1.07 s) — in `Rider_Fly_Lean.fbx`
- Rider_Fly_Stand_R (1.07 s) — in `Rider_Fly_Stand R.fbx`

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Idles (2)

- Rider_Idle_Decline (3.27 s) — in `Rider_Idle_Incline_Decline.FBX`
- Rider_Idle_Incline (3.30 s) — in `Rider_Idle_Incline_Decline.FBX`

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Jumps (4)

- Rider_Canter_Land (0.63 s) — in `Rider_Jump_Canter.FBX`
- Rider_Forward_land (0.47 s) — in `Rider_Jump_Forward.FBX`
- Rider_Gallop_Land (0.57 s) — in `Rider_Jump_Gallop.FBX`
- Rider_Sprint_Land (0.63 s) — in `Rider_Jump_Sprint.FBX`

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Locomotion (20)

- Rider_Canter_Left (0.67 s)
- Rider_Canter_Right (0.67 s)
- Rider_Gallop_Left (0.60 s)
- Rider_Gallop_Right (0.60 s)
- Rider_Sprint_Left (0.53 s)
- Rider_Sprint_Right (0.53 s)
- Rider_Trot_Left (0.80 s)
- Rider_Trot_Pace (0.93 s)
- Rider_Trot_Right (0.80 s)
- Rider_Turn_Left (1.07 s)
- Rider_Turn_Neigh_Left (1.40 s)
- Rider_Turn_Neigh_Right (1.40 s)
- Rider_Turn_Right (1.07 s)
- Rider_WalkBack (1.07 s)
- Rider_WalkBack_Left (1.07 s)
- Rider_WalkBack_Right (1.07 s)
- Rider_Walk_Down (1.07 s)
- Rider_Walk_Left (1.07 s)
- Rider_Walk_Right (1.07 s)
- Rider_Walk_Up (1.07 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Mount Dismount (23)

- Rider_Dismount_Back (1.00 s) — in `Rider_Mount_Dismount_Back.FBX`
- Rider_Dismount_Carriage_Left (2.93 s) — in `Rider_Mount_Dismount_Carriage_Left.FBX`
- Rider_Dismount_Carriage_Right (2.90 s) — in `Rider_Mount_Dismount_Carriage_Right.FBX`
- Rider_Dismount_Cart_Left (1.63 s) — in `Rider_Mount_Dismount_Cart_Left.FBX`
- Rider_Dismount_Cart_Right (1.63 s) — in `Rider_Mount_Dismount_Cart_Right.FBX`
- Rider_Dismount_Dragon_Left (1.97 s) — in `Rider_Mount_Dismount_Dragon.FBX`
- Rider_Dismount_Dragon_Right (1.97 s) — in `Rider_Mount_Dismount_Dragon.FBX`
- Rider_Dismount_Elephant_Left (2.67 s) — in `Rider_Mount_Dismount_Elephant_Left.fbx`
- Rider_Dismount_Elephant_Right (2.67 s) — in `Rider_Mount_Dismount_Elephant_Left.fbx`
- Rider_Dismount_Front_Left (1.13 s) — in `Rider_Mount_Dismount_Front_Left.FBX`
- Rider_Dismount_Front_Right (1.13 s) — in `Rider_Mount_Dismount_Front_Right.FBX`
- Rider_Mount_Above (1.20 s)
- Rider_Mount_Back (1.00 s) — in `Rider_Mount_Dismount_Back.FBX`
- Rider_Mount_Carriage_Left (2.60 s) — in `Rider_Mount_Dismount_Carriage_Left.FBX`
- Rider_Mount_Carriage_Right (2.60 s) — in `Rider_Mount_Dismount_Carriage_Right.FBX`
- Rider_Mount_Cart_Left (1.67 s) — in `Rider_Mount_Dismount_Cart_Left.FBX`
- Rider_Mount_Cart_Right (1.67 s) — in `Rider_Mount_Dismount_Cart_Right.FBX`
- Rider_Mount_Dragon_Left (2.00 s) — in `Rider_Mount_Dismount_Dragon.FBX`
- Rider_Mount_Dragon_Right (2.00 s) — in `Rider_Mount_Dismount_Dragon.FBX`
- Rider_Mount_Elephant_Left (2.60 s) — in `Rider_Mount_Dismount_Elephant_Left.fbx`
- Rider_Mount_Elephant_Right (2.60 s) — in `Rider_Mount_Dismount_Elephant_Left.fbx`
- Rider_Mount_Front_Left (1.33 s) — in `Rider_Mount_Dismount_Front_Left.FBX`
- Rider_Mount_Front_Right (1.33 s) — in `Rider_Mount_Dismount_Front_Right.FBX`

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Strafe (6)

- Rider_Canter_Strafe_Forward_Left (0.67 s)
- Rider_Canter_Strafe_Forward_Right (0.67 s)
- Rider_Canter_Strafe_Left (0.67 s)
- Rider_Canter_Strafe_Right (0.67 s)
- Rider_Walk_Strafe_Left (1.07 s)
- Rider_Walk_Strafe_Right (1.07 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Swim (7)

- Rider_Swim_Back (1.07 s)
- Rider_Swim_Back_Left (1.07 s)
- Rider_Swim_Back_Right (1.07 s)
- Rider_Swim_Left (1.07 s)
- Rider_Swim_Right (1.07 s)
- Rider_Swim_Turn_Left_90 (1.00 s)
- Rider_Swim_Turn_Right_90 (1.00 s)

### Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Rider/Weapons While Riding (73)

- Bow Fire (0.10 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold E 0 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold E 1 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold E 1 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold E 2 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold E 2 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold E 3 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold E 3 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold E 4 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold E 4 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold N 0 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold N 1 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold N 1 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold N 2 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold N 2 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold N 3 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold N 3 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold N 4 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold N 4 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold S 0 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold S 1 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold S 1 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold S 2 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold S 2 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold S 3 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold S 3 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold S 4 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold S 4 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold W 0 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold W 1 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold W 1 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold W 2 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold W 2 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold W 3 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold W 3 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Hold W 4 (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Hold W 4 (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Imposible Angle (0.00 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Imposible Angle (0.00 s) — in `Rider_Weapon_Bow.FBX`
- Bow Prepare E (0.90 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Prepare N (0.93 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Prepare S (0.93 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Prepare W (0.90 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Release E (1.03 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Release N (1.03 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Release S (1.03 s) — in `Rider_Weapon_Bow LH.FBX`
- Bow Release W (1.03 s) — in `Rider_Weapon_Bow LH.FBX`
- Pistol Reload Left (3.00 s) — in `Rider_Weapon_Gun_Reload.FBX`
- Pistol Reload Right (3.00 s) — in `Rider_Weapon_Gun_Reload.FBX`
- Rider Gun Aim +S (0.00 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Aim +S Opp (0.00 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Rider Gun Aim -S (0.00 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Aim -S Opp (0.00 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Rider Gun Aim E (0.00 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Aim E Opp (0.00 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Rider Gun Aim N (0.00 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Aim N Opp (0.00 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Rider Gun Aim W (0.00 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Aim W Opp (0.00 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Rider Gun Fire +S R (0.27 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Fire +S R Opp (0.27 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Rider Gun Fire -S R (0.27 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Fire -S R Opp (0.27 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Rider Gun Fire E R (0.27 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Fire E R Opp (0.27 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Rider Gun Fire N R (0.27 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Fire N R Opp (0.27 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Rider Gun Fire W R (0.27 s) — in `Rider_Weapon_Gun.FBX`
- Rider Gun Fire W R Opp (0.27 s) — in `Rider_Weapon_Gun Mirror.FBX`
- Sword Attack Left Backward LH (0.80 s) — in `Rider_Weapon_Sword.FBX`
- Sword Attack Left Forward LH (0.80 s) — in `Rider_Weapon_Sword.FBX`
- Sword Attack Right Backward LH (0.80 s) — in `Rider_Weapon_Sword.FBX`
- Sword Attack Right Forward LH (0.83 s) — in `Rider_Weapon_Sword.FBX`

### Packages/com.unity.timeline/Editor/StyleSheets/res (1)

- HumanoidDefault (0.03 s)

### Quaternius/Universal Animation Library (77)

- A_TPose (2.50 s) — in `UAL1_Standard.fbx`
- Armature|A_TPose (2.50 s) — in `UAL1_Standard_RM.fbx`
- Armature|Crouch_Fwd_Loop (2.00 s) — in `UAL1_Standard_RM.fbx`
- Armature|Crouch_Idle_Loop (2.93 s) — in `UAL1_Standard_RM.fbx`
- Armature|Dance_Loop (1.00 s) — in `UAL1_Standard_RM.fbx`
- Armature|Death01 (2.40 s) — in `UAL1_Standard_RM.fbx`
- Armature|Driving_Loop (1.67 s) — in `UAL1_Standard_RM.fbx`
- Armature|Fixing_Kneeling (5.20 s) — in `UAL1_Standard_RM.fbx`
- Armature|Hit_Chest (0.33 s) — in `UAL1_Standard_RM.fbx`
- Armature|Hit_Head (0.43 s) — in `UAL1_Standard_RM.fbx`
- Armature|Idle_Loop (2.50 s) — in `UAL1_Standard_RM.fbx`
- Armature|Idle_Talking_Loop (2.93 s) — in `UAL1_Standard_RM.fbx`
- Armature|Idle_Torch_Loop (1.27 s) — in `UAL1_Standard_RM.fbx`
- Armature|Interact (2.00 s) — in `UAL1_Standard_RM.fbx`
- Armature|Jog_Fwd_Loop (0.93 s) — in `UAL1_Standard_RM.fbx`
- Armature|Jump_Land (1.27 s) — in `UAL1_Standard_RM.fbx`
- Armature|Jump_Loop (2.50 s) — in `UAL1_Standard_RM.fbx`
- Armature|Jump_Start (1.33 s) — in `UAL1_Standard_RM.fbx`
- Armature|PickUp_Table (0.83 s) — in `UAL1_Standard_RM.fbx`
- Armature|Pistol_Aim_Down (0.17 s) — in `UAL1_Standard_RM.fbx`
- Armature|Pistol_Aim_Neutral (0.17 s) — in `UAL1_Standard_RM.fbx`
- Armature|Pistol_Aim_Up (0.17 s) — in `UAL1_Standard_RM.fbx`
- Armature|Pistol_Idle_Loop (1.67 s) — in `UAL1_Standard_RM.fbx`
- Armature|Pistol_Reload (1.67 s) — in `UAL1_Standard_RM.fbx`
- Armature|Pistol_Shoot (0.63 s) — in `UAL1_Standard_RM.fbx`
- Armature|Punch_Cross (1.00 s) — in `UAL1_Standard_RM.fbx`
- Armature|Punch_Jab (0.87 s) — in `UAL1_Standard_RM.fbx`
- Armature|Push_Loop (2.67 s) — in `UAL1_Standard_RM.fbx`
- Armature|Roll (1.47 s) — in `UAL1_Standard_RM.fbx`
- Armature|Sitting_Enter (1.30 s) — in `UAL1_Standard_RM.fbx`
- Armature|Sitting_Exit (1.03 s) — in `UAL1_Standard_RM.fbx`
- Armature|Sitting_Idle_Loop (1.67 s) — in `UAL1_Standard_RM.fbx`
- Armature|Sitting_Talking_Loop (2.93 s) — in `UAL1_Standard_RM.fbx`
- Armature|Spell_Simple_Enter (0.53 s) — in `UAL1_Standard_RM.fbx`
- Armature|Spell_Simple_Exit (0.43 s) — in `UAL1_Standard_RM.fbx`
- Armature|Spell_Simple_Idle_Loop (2.10 s) — in `UAL1_Standard_RM.fbx`
- Armature|Spell_Simple_Shoot (0.50 s) — in `UAL1_Standard_RM.fbx`
- Armature|Sprint_Loop (0.67 s) — in `UAL1_Standard_RM.fbx`
- Armature|Swim_Fwd_Loop (1.33 s) — in `UAL1_Standard_RM.fbx`
- Armature|Swim_Idle_Loop (3.33 s) — in `UAL1_Standard_RM.fbx`
- Armature|Sword_Attack (1.53 s) — in `UAL1_Standard_RM.fbx`
- Armature|Sword_Idle (1.67 s) — in `UAL1_Standard_RM.fbx`
- Armature|Walk_Formal_Loop (1.33 s) — in `UAL1_Standard_RM.fbx`
- Armature|Walk_Loop (1.33 s) — in `UAL1_Standard_RM.fbx`
- Crouch_Fwd_Loop (2.00 s) — in `UAL1_Standard.fbx`
- Dance_Loop (1.00 s) — in `UAL1_Standard.fbx`
- Death01 (2.40 s) — in `UAL1_Standard.fbx`
- Driving_Loop (1.67 s) — in `UAL1_Standard.fbx`
- Fixing_Kneeling (5.20 s) — in `UAL1_Standard.fbx`
- Idle_Loop (2.50 s) — in `UAL1_Standard.fbx`
- Idle_Talking_Loop (2.93 s) — in `UAL1_Standard.fbx`
- Idle_Torch_Loop (1.27 s) — in `UAL1_Standard.fbx`
- Interact (2.00 s) — in `UAL1_Standard.fbx`
- Jump_Land (1.27 s) — in `UAL1_Standard.fbx`
- Jump_Loop (2.50 s) — in `UAL1_Standard.fbx`
- Jump_Start (1.33 s) — in `UAL1_Standard.fbx`
- PickUp_Table (0.83 s) — in `UAL1_Standard.fbx`
- Pistol_Aim_Down (0.17 s) — in `UAL1_Standard.fbx`
- Pistol_Aim_Neutral (0.17 s) — in `UAL1_Standard.fbx`
- Pistol_Aim_Up (0.17 s) — in `UAL1_Standard.fbx`
- Pistol_Idle_Loop (1.67 s) — in `UAL1_Standard.fbx`
- Pistol_Reload (1.67 s) — in `UAL1_Standard.fbx`
- Pistol_Shoot (0.63 s) — in `UAL1_Standard.fbx`
- Push_Loop (2.67 s) — in `UAL1_Standard.fbx`
- Sitting_Enter (1.30 s) — in `UAL1_Standard.fbx`
- Sitting_Exit (1.03 s) — in `UAL1_Standard.fbx`
- Sitting_Idle_Loop (1.67 s) — in `UAL1_Standard.fbx`
- Sitting_Talking_Loop (2.93 s) — in `UAL1_Standard.fbx`
- Spell_Simple_Enter (0.53 s) — in `UAL1_Standard.fbx`
- Spell_Simple_Exit (0.43 s) — in `UAL1_Standard.fbx`
- Spell_Simple_Idle_Loop (2.10 s) — in `UAL1_Standard.fbx`
- Spell_Simple_Shoot (0.50 s) — in `UAL1_Standard.fbx`
- Sprint_Loop (0.67 s) — in `UAL1_Standard.fbx`
- Swim_Fwd_Loop (1.33 s) — in `UAL1_Standard.fbx`
- Swim_Idle_Loop (3.33 s) — in `UAL1_Standard.fbx`
- Sword_Idle (1.67 s) — in `UAL1_Standard.fbx`
- Walk_Formal_Loop (1.33 s) — in `UAL1_Standard.fbx`

### Quaternius/Universal Animation Library 2 (21)

- A_TPose (2.50 s) — in `UAL2_Standard.fbx`
- Chest_Open (1.37 s) — in `UAL2_Standard.fbx`
- ClimbUp_1m (0.67 s) — in `UAL2_Standard.fbx`
- Farm_Harvest (2.50 s) — in `UAL2_Standard.fbx`
- Farm_PlantSeed (2.77 s) — in `UAL2_Standard.fbx`
- Farm_Watering (3.80 s) — in `UAL2_Standard.fbx`
- Idle_FoldArms_Loop (2.50 s) — in `UAL2_Standard.fbx`
- Idle_Lantern_Loop (2.50 s) — in `UAL2_Standard.fbx`
- Idle_No_Loop (2.50 s) — in `UAL2_Standard.fbx`
- Idle_Rail_Call (2.50 s) — in `UAL2_Standard.fbx`
- Idle_Rail_Loop (2.50 s) — in `UAL2_Standard.fbx`
- Idle_TalkingPhone_Loop (2.93 s) — in `UAL2_Standard.fbx`
- NinjaJump_Idle_Loop (2.00 s) — in `UAL2_Standard.fbx`
- NinjaJump_Land (1.27 s) — in `UAL2_Standard.fbx`
- NinjaJump_Start (0.97 s) — in `UAL2_Standard.fbx`
- Slide_Loop (2.00 s) — in `UAL2_Standard.fbx`
- Sword_Heavy_Combo (4.33 s) — in `UAL2_Standard.fbx`
- Sword_Regular_Combo (3.00 s) — in `UAL2_Standard.fbx`
- TreeChopping_Loop (0.97 s) — in `UAL2_Standard.fbx`
- Walk_Carry_Loop (2.00 s) — in `UAL2_Standard.fbx`
- Yes (2.50 s) — in `UAL2_Standard.fbx`

### Starter Runtime/SpaceRobotKyle/Models (1)

- KyleStatic (0.02 s) — in `KyleRobot.fbx`

### Starter Runtime/ThirdPersonController/Character/Animations (1)

- Run_S (0.63 s) — in `Locomotion--Run_S.anim.fbx`

### StylizedCharacter/Animations/Character/Human/Female (5)

- Combat_2H_Attack03 (1.13 s) — in `A_Hu_F_BasePack.fbx`
- Idle (2.23 s) — in `A_Hu_F_IdlePreview.fbx`
- Idle_2HLStance (2.33 s) — in `A_Hu_F_IdlePreview.fbx`
- Idle_2HStance (2.33 s) — in `A_Hu_F_IdlePreview.fbx`
- Idle_WideArms (2.23 s) — in `A_Hu_F_IdlePreview.fbx`

### StylizedCharacter/Animations/Character/Human/Male (4)

- Idle_2H (2.00 s) — in `A_Hu_M_IdlePreview.fbx`
- Idle_2HL (2.00 s) — in `A_Hu_M_IdlePreview.fbx`
- Idle_WideArms (2.27 s) — in `A_Hu_M_IdlePreview.fbx`
- idle (2.27 s) — in `A_Hu_M_IdlePreview.fbx`

### StylizedCharacter/Meshes/Item/Equipment/Boots (1)

- T_Pose (1.67 s) — in `SK_Hu_F_Boots_Peasant.fbx`

### StylizedCharacter/Meshes/Item/Equipment/Cape (1)

- T_Pose (1.67 s) — in `SK_Hu_F_Cape_Peasant.fbx`

### StylizedCharacter/Meshes/Item/Equipment/Chest (3)

- T_Pose (1.67 s) — in `SK_Hu_F_Chest_Peasant_01.fbx`
- T_Pose (1.67 s) — in `SK_Hu_F_Chest_Peasant_02.fbx`
- T_Pose (1.67 s) — in `SK_Hu_F_Chest_Peasant_03.fbx`

### StylizedCharacter/Meshes/Item/Equipment/ChestAdd (1)

- T_Pose (1.67 s) — in `SK_Hu_F_ChestAdd_Peasant.fbx`

### VIVID_Arts/Shared/Animations (1)

- Camera Shake_03 (26.65 s)

### _Project/Art/Animations/Custom (1)

- GroundSmash (5.03 s)

### _Project/Art/Enemies/Downloaded/Limule (4)

- Attack.001 (0.63 s) — in `Limule.fbx`
- Attack.002 (0.63 s) — in `Limule.fbx`
- Left (1.04 s) — in `Limule.fbx`
- Right (1.04 s) — in `Limule.fbx`

### _Project/Art/Enemies/Downloaded/Treant1 (6)

- Attack2 (1.04 s) — in `Treant1Clips.fbx`
- Attack3 (1.25 s) — in `Treant1Clips.fbx`
- Attack_1 (1.67 s) — in `Treant1Clips.fbx`
- Death2 (1.17 s) — in `Treant1Clips.fbx`
- Death3 (1.17 s) — in `Treant1Clips.fbx`
- River Dance (1.67 s) — in `Treant1Clips.fbx`

### _Project/Art/Enemies/Downloaded/Treant2 (4)

- Attack2 (1.04 s) — in `Treant2Clips.fbx`
- Death2 (1.17 s) — in `Treant2Clips.fbx`
- Death3 (1.17 s) — in `Treant2Clips.fbx`
- River Dance (1.67 s) — in `Treant2Clips.fbx`

### _Project/Art/Enemies/Downloaded/VampireBat (1)

- Bat_Idle (0.33 s) — in `VampireBat.fbx`

### _Project/Art/Enemies/Downloaded/Wolf (6)

- Death (1.07 s) — in `Wolf.fbx`
- Eating (2.53 s) — in `Wolf.fbx`
- Gallop_Jump (0.93 s) — in `Wolf.fbx`
- Idle_2 (3.33 s) — in `Wolf.fbx`
- Idle_HitReact_Right (0.67 s) — in `Wolf.fbx`
- Jump_ToIdle (1.33 s) — in `Wolf.fbx`

### _Project/Art/Wildlife/Deer (6)

- Attack_Headbutt (0.80 s) — in `Deer.fbx`
- Attack_Kick (0.80 s) — in `Deer.fbx`
- Gallop_Jump (1.40 s) — in `Deer.fbx`
- Idle_2 (3.33 s) — in `Deer.fbx`
- Idle_HitReact_Right (0.53 s) — in `Deer.fbx`
- Jump_toIdle (1.53 s) — in `Deer.fbx`

### _Project/Art/Wildlife/Fox (6)

- Attack (1.33 s) — in `Fox.fbx`
- Gallop_Jump (0.93 s) — in `Fox.fbx`
- Idle_2 (3.33 s) — in `Fox.fbx`
- Idle_2_HeadLow (4.00 s) — in `Fox.fbx`
- Idle_HitReact_Right (0.67 s) — in `Fox.fbx`
- Jump_ToIdle (1.33 s) — in `Fox.fbx`

### _Project/Art/Wildlife/Stag (7)

- Attack_Headbutt (0.80 s) — in `Stag.fbx`
- Attack_Kick (0.80 s) — in `Stag.fbx`
- Gallop_Jump (1.40 s) — in `Stag.fbx`
- Idle_2 (3.33 s) — in `Stag.fbx`
- Idle_Headlow (3.33 s) — in `Stag.fbx`
- Idle_HitReact_Right (0.53 s) — in `Stag.fbx`
- Jump_toIdle (1.53 s) — in `Stag.fbx`
