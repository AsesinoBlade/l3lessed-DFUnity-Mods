using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Wenzil.Console;

namespace AmbidexterityModule
{
    public class AmbidexterityManager : MonoBehaviour
    {
        //initiates mod instances for mod manager.
        public static Mod mod;
        public static AmbidexterityManager AmbidexterityManagerInstance;
        static ModSettings settings;
        public static ConsoleController consoleController;
        //sets up console instances for the script to load the objects into. Used to disabled texture when consoles open.
        static GameObject console;
        //sets up dfAudioSource object to attach objects to and play sound from said object.
        public static DaggerfallAudioSource dfAudioSource;

        public static int playerLayerMask;

        int[] randomattack;

        //block key.
        public static string offHandKeyString;

        //sets current equipment state based on whats equipped in what hands.
        public static int equipState; //0 - nothing equipped | 1 - Main hand equipped + melee | 2 - Off hand equipped + melee | 3 - Weapon + Shield | 4 - two-handed | 5 - duel wield | 6 - Bow.
        private static int attackState;
        public static int attackerDamage;
        public int[] prohibitedWeapons = { 120, 121, 122, 123, 125, 126, 128 };

        //returns current Attackstate based on each weapon state.
        //0 - Both hands idle | 7 - Either hand is parrying | (ANY OTHER NUMBER) - Normal attack state number for current swinging weapon.
        public static int AttackState { get { return checkAttackState(); } set { attackState = value; } }
        // 0 - both hands idle/Not parrying at all | 1 - parrying with a weapon.
        public static int ParryState { get { return checkParryState(); } }

        //mod setting manipulation values.
        public static float BlockTimeMod { get; private set; }
        public static float BlockCostMod { get; private set; }
        public float AttackPrimerTime { get; private set; }
        private float EquipCountdownMainHand;
        private float EquipCountdownOffHand;

        float cooldownTime;
        private bool arrowLoading;

        //Use for keyinput routine. Stores how long since an attack key was pressed last.
        private float timePass;

        Queue<int> playerInput = new Queue<int>();

        //mode setting triggers.
        public static bool toggleBob;
        public static bool bucklerMechanics;
        public static bool classicAnimations;
        public static bool physicalWeapons;
        public static bool usingMainhand;
        //triggers for CalculateAttackFormula to ensure hits are registered and trigger corresponding script code blocks.
        public static bool isHit = false;
        public static bool isIdle = true;
        //key input manager triggers.
        private bool parryKeyPressed;
        private bool attackKeyPressed;
        private bool offHandKeyPressed;
        private bool handKeyPressed;
        private bool mainHandKeyPressed;
        //stores an instance of usingRightHand for checking if left or right hand is being used/set.
        public static bool usingRightHand;

        //stores below objects for CalculateAttackFormula to ensure mod scripts register who is attacking and being attacked and trigger proper code blocks.
        public static DaggerfallEntity attackerEntity;
        public static DaggerfallEntity targetEntity;

        //stores and instance of current equipped weapons.
        private DaggerfallUnityItem mainHandItem;
        private DaggerfallUnityItem offHandItem;
        public DaggerfallUnityItem currentmainHandItem;
        public DaggerfallUnityItem currentoffHandItem;

        //keycode object to store block key code value.
        public static KeyCode offHandKeyCode;
        private RacialOverrideEffect racialOverride;
        private bool reset;
        public static GameObject mainCamera;

        static Vector3 debugcast;

        //particle system empty objects.
        public GameObject SparkPreb;
        public static ParticleSystem sparkParticles;
        public static bool assets;

       //starts mod manager on game begin. Grabs mod initializing paramaters.
       //ensures SateTypes is set to .Start for proper save data restore values.
       [Invoke(StateManager.StateTypes.Game, 0)]
        public static void Init(InitParams initParams)
        {
            //Below code blocks set up instances of class/script/mod.\\
            //sets up and runs this script file as the main mod file, so it can setup all the other scripts for the mod.
            GameObject AmbidexterityManager = new GameObject("AmbidexterityManager");
            AmbidexterityManagerInstance = AmbidexterityManager.AddComponent<AmbidexterityManager>();
            Debug.Log("You pull all your equipment out and begin preparing for the journey ahead.");

            //BEGINS ATTACHING EACH SCRIPT TO THE MOD INSTANCE\\
            //attaches and starts shield controller script.
            GameObject FPSShieldObject = new GameObject("FPSShield");
            FPSShield.FPSShieldInstance = FPSShieldObject.AddComponent<FPSShield>();
            Debug.Log("Shield harness checked & equipped.");

            //attaches and starts alternate FPSWeapon controller script.
            GameObject AltFPSWeaponObject = new GameObject("AltFPSWeapon");
            AltFPSWeapon.AltFPSWeaponInstance = AltFPSWeaponObject.AddComponent<AltFPSWeapon>();
            Debug.Log("offhand Weapon checked & equipped.");

            //attaches and starts the off hand weapon controller script.
            GameObject OffHandFPSWeaponObject = new GameObject("OffHandFPSWeapon");
            OffHandFPSWeapon.OffHandFPSWeaponInstance = OffHandFPSWeaponObject.AddComponent<OffHandFPSWeapon>();
            Debug.Log("Main weapon checked.");

            //attaches and starts the ShieldFormulaHelperObject to run the parry and shield CalculateAttackDamage mod hook adjustments
            GameObject ShieldFormulaHelperObject = new GameObject("ShieldFormulaHelper");
            ShieldFormulaHelper.ShieldFormulaHelperInstance = ShieldFormulaHelperObject.AddComponent<ShieldFormulaHelper>();
            Debug.Log("Weapons sharpened, cleaned, and equipped.");

            //initiates mod paramaters for class/script.
            mod = initParams.Mod;
            //loads mods settings.
            settings = mod.GetSettings();
            //assets = mod.LoadAllAssetsFromBundle();
            //initiates save paramaters for class/script.
            //mod.SaveDataInterface = instance;
            //after finishing, set the mod's IsReady flag to true.
            mod.IsReady = true;
            Debug.Log("You stretch your arms, cracking your knuckles, your whole body ready and alert.");

            usingRightHand = GameManager.Instance.WeaponManager.UsingRightHand;
        }

        // Use this for initialization
        void Start()
        {
            //assigns console to script object, then attaches the controller object to that.
            console = GameObject.Find("Console");
            consoleController = console.GetComponent<ConsoleController>();
            SparkPreb = mod.GetAsset<GameObject>("Spark_Particles");
            //SparkPreb = Resources.Load("Particles/Spark_Particles") as GameObject;
            sparkParticles = SparkPreb.GetComponent<ParticleSystem>();

            //finds daggerfall audio source object, loads it, and then adds it to the player object, so it knows where the sound source is from.
            dfAudioSource = GameManager.Instance.PlayerObject.AddComponent<DaggerfallAudioSource>();

            //check if player has left handed enabled and set in scripts.
            FPSShield.flip = GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal;
            OffHandFPSWeapon.flip = GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal;
            AltFPSWeapon.flip = GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal;

            //assigns the main camera engine object to mainCamera general object. Used to detect shield knock back directions.
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));

            //*THIS NEEDS CLEANED UP. CAN USE A SINGLE INSTANCE OF THIS IN MANAGER FILE*
            OffHandFPSWeapon.dfUnity = DaggerfallUnity.Instance;
            AltFPSWeapon.dfUnity = DaggerfallUnity.Instance;
            //binds mod settings to script properties.
            offHandKeyString = settings.GetValue<string>("Settings", "offHandKeyString");
            BlockTimeMod = settings.GetValue<float>("Settings", "BlockTimeMod");
            BlockCostMod = settings.GetValue<float>("Settings", "BlockCostMod");
            AttackPrimerTime = settings.GetValue<float>("Settings", "AttackPrimerTime");
            toggleBob = settings.GetValue<bool>("Settings", "ToggleBob");
            bucklerMechanics = settings.GetValue<bool>("Settings", "BucklerMechanics");
            classicAnimations = settings.GetValue<bool>("Settings", "ClassicAnimations");
            physicalWeapons = settings.GetValue<bool>("Settings", "PhysicalWeapons");

            Debug.Log("You're equipment is setup, and you feel limber and ready for anything.");

            //If not using classic animations, this limits the types of attacks and is central to ensuring the smooth animation system I'm working on can function correctly.
            if(!classicAnimations)
                randomattack = new int[] { 1, 3, 4, 6 };
            //else revert to normal attack range.
            else
                randomattack = new int[] { 1, 2, 3, 4, 5, 6};


            //register the formula calculate attack damage formula so can pull attack properties needed and zero out damage when player is blocking succesfully.
            //**MODDERS: This is the formula override you need to replace within your mod to ensure the shield script works properly**\\
            FormulaHelper.RegisterOverride(mod, "CalculateAttackDamage", (Func<DaggerfallEntity, DaggerfallEntity, int, int, DaggerfallUnityItem, int>)ShieldFormulaHelper.CalculateAttackDamage);

            //converts string key setting into valid unity keycode. Ensures mouse and keyboard inputs work properly.
            offHandKeyCode = (KeyCode)Enum.Parse(typeof(KeyCode), offHandKeyString);

            //defaults both weapons to melee/null for loading safety. Weapons update on load of save.
            OffHandFPSWeapon.WeaponType = WeaponTypes.Melee;
            OffHandFPSWeapon.MetalType = MetalTypes.None;
            AltFPSWeapon.WeaponType = WeaponTypes.Melee;
            AltFPSWeapon.MetalType = MetalTypes.None;
        }

        private void Update()
        {
            //ensures if weapons aren't showing, or consoles open, or games paused, or its loading, or the user opened any interfaces at all, that nothing is done.
            if (GameManager.Instance.WeaponManager.Sheathed || consoleController.ui.isConsoleOpen || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress || DaggerfallUI.UIManager.WindowCount != 0)
            {
                return; //show nothing.
            }

            // Do nothing if player paralyzed or is climbing
            if (GameManager.Instance.PlayerEntity.IsParalyzed || GameManager.Instance.ClimbingMotor.IsClimbing)
            {
                OffHandFPSWeapon.OffHandWeaponShow = false;
                AltFPSWeapon.AltFPSWeaponShow = false;
                FPSShield.shieldEquipped = false;
                GameManager.Instance.WeaponManager.ScreenWeapon.ShowWeapon = false;
                return;
            }

            // Hide weapons and do nothing if spell is ready or cast animation in progress
            if (GameManager.Instance.PlayerEffectManager)
            {
                if (GameManager.Instance.PlayerEffectManager.HasReadySpell || GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                {
                    if (AttackState == 0 && InputManager.Instance.ActionStarted(InputManager.Actions.ReadyWeapon))
                    {
                        GameManager.Instance.PlayerEffectManager.AbortReadySpell();

                        //if currently unsheathed, then sheath it, so we can give the effect of unsheathing it again
                        if (!GameManager.Instance.WeaponManager.Sheathed)
                            GameManager.Instance.WeaponManager.Sheathed = true;
                    }
                    else
                    {
                        OffHandFPSWeapon.OffHandWeaponShow = false;
                        AltFPSWeapon.AltFPSWeaponShow = false;
                        FPSShield.shieldEquipped = false;
                        return;
                    }
                }
            }

            //grab and assign current weapon manager equipment times to ensure classic equipment mechanics/rendering works.
            EquipCountdownMainHand = GameManager.Instance.WeaponManager.EquipCountdownRightHand;
            EquipCountdownOffHand = GameManager.Instance.WeaponManager.EquipCountdownLeftHand;

            //Begin monitoring for key input, updating hands and all related properties, and begin monitoring for key presses and/or property/state changes.
            //small routine to check for attack key inputs and start a short delay timer if detected.
            //this makes using parry easier by giving a delay time frame to click both attack buttons.
            //it also allows players to load up a second attack and skip the .16f wind up, priming.
            KeyPressCheck();

            // swap weapon hand.
            if (AttackState == 0 && InputManager.Instance.ActionComplete(InputManager.Actions.SwitchHand))
                ToggleHand();

            //checks to ensure equipment is the same, and if so, moves on. If not, updates the players equip state to ensure all script bool triggers are properly set to handle
            //each script and its corresponding animation systems.
            UpdateHands();

            Debug.Log(isHit.ToString() + " | " + AttackState.ToString() + " | " + equipState.ToString());

            //CONTROLS PARRY ANIMATIONS AND WEAPON STATES\\
            //if player is hit and they are parrying do...
            if (isHit && AttackState == 7)
            {
                //uses the Particle System Container class to setup and grab the prefab spark particle emitter constructed in the class already.
                //then assigns it to a container particle system for later use.
                Destroy(Instantiate(sparkParticles, attackerEntity.EntityBehaviour.transform.position + (attackerEntity.EntityBehaviour.transform.forward * .35f), Quaternion.identity, null), 1.0f);

                //if duel wield is equipped stop offhand parry animation and start swing two frames in.
                if (equipState == 5 || (equipState == 4 && !GameManager.Instance.WeaponManager.UsingRightHand))
                {
                    //stops parry animation
                    StopCoroutine(OffHandFPSWeapon.ParryCoroutine);
                    OffHandFPSWeapon.ResetAnimation();
                    //assigns attack state.
                    OffHandFPSWeapon.weaponState = (WeaponStates)3;
                    //starts attack state two frames in.
                    StartCoroutine(OffHandFPSWeapon.AnimationCalculator(0, 0, 0, 0, false, 1, 0, .4f));
                    return;
                }
                //if duel wield or one handed is equipped stop main hand parry animation and start swing two frames in.
                if (equipState == 1 || equipState == 4)
                {
                    //stops parry animation
                    StopCoroutine(AltFPSWeapon.ParryCoroutine);
                    AltFPSWeapon.ResetAnimation();
                    //assigns attack state.
                    AltFPSWeapon.weaponState = (WeaponStates)4;
                    //starts attack state two frames in.
                    StartCoroutine(AltFPSWeapon.AnimationCalculator(0, 0, 0, 0, false, 1, 0, .4f));
                    return;
                }
            }            
        }

        //controls the parry and its related animations. Ensures proper parry animation is ran.
        void Parry()
        {
            Debug.Log("1");
            //sets weapon state to parry.
            if ((equipState == 5 || equipState == 2 || (equipState == 4 && !GameManager.Instance.WeaponManager.UsingRightHand)) && OffHandFPSWeapon.weaponState == WeaponStates.Idle && ParryState == 0 && (OffHandFPSWeapon.WeaponType != WeaponTypes.Melee || OffHandFPSWeapon.WeaponType != WeaponTypes.Bow))
            {
                AttackState = 7;
                //sets offhand weapon to parry state, starts classic animation update system, and plays swing sound.
                OffHandFPSWeapon.isParrying = true;
                OffHandFPSWeapon.ParryCoroutine = StartCoroutine(OffHandFPSWeapon.AnimationCalculator(0, -.25f, .75f, -.5f, true, .5f, 0, 0, true));
                OffHandFPSWeapon.PlaySwingSound();               
                return;
            }

            if ((equipState == 1 || (equipState == 4 && GameManager.Instance.WeaponManager.UsingRightHand)) && AltFPSWeapon.weaponState == WeaponStates.Idle && ParryState == 0 && (AltFPSWeapon.WeaponType != WeaponTypes.Melee || AltFPSWeapon.WeaponType != WeaponTypes.Bow))
            {
                Debug.Log("2");
                attackState = 7;
                //sets main weapon to parry state, starts classic animation update system, and plays swing sound.
                AltFPSWeapon.isParrying = true;
                AltFPSWeapon.ParryCoroutine = StartCoroutine(AltFPSWeapon.AnimationCalculator(0, -.25f, .75f, -.5f, true, .5f, 0, 0, false));
                OffHandFPSWeapon.PlaySwingSound();               
                return;
            }
        }

        //controls main hand attack and ensures it can't be spammed/bugged.
        void MainAttack()
        {
            if (AltFPSWeapon.AltFPSWeaponShow && AltFPSWeapon.weaponState == WeaponStates.Idle && ParryState == 0)
            {
                //if the player has a shield equipped, and it is not being used, let them attack.
                if (FPSShield.shieldEquipped && (FPSShield.shieldStates == 0 || FPSShield.shieldStates == 8 || !FPSShield.isBlocking))
                {
                    //sets shield state to weapon attacking, which activates corresponding` coroutines and animations.
                    FPSShield.shieldStates = 7;
                    GameManager.Instance.PlayerEntity.DecreaseFatigue(11);
                    attackState = randomattack[UnityEngine.Random.Range(0, randomattack.Length)];
                    AltFPSWeapon.weaponState = (WeaponStates)attackState;
                    GameManager.Instance.WeaponManager.ScreenWeapon.PlaySwingSound();
                    StartCoroutine(AltFPSWeapon.AnimationCalculator());
                    Debug.Log(AltFPSWeapon.weaponState.ToString());
                    TallyCombatSkills(currentmainHandItem);
                    return;
                }

                //if the player does not have a shield equipped, let them attack.
                if (!FPSShield.shieldEquipped && ParryState == 0)
                {
                    //both weapons are idle, then perform attack routine....
                    if (OffHandFPSWeapon.weaponState == WeaponStates.Idle && AltFPSWeapon.weaponState == WeaponStates.Idle)
                    {
                        attackState = randomattack[UnityEngine.Random.Range(0, randomattack.Length)];
                        AltFPSWeapon.weaponState = (WeaponStates)attackState;
                        GameManager.Instance.WeaponManager.ScreenWeapon.PlaySwingSound();
                        GameManager.Instance.PlayerEntity.DecreaseFatigue(11);
                        StartCoroutine(AltFPSWeapon.AnimationCalculator());
                        Debug.Log(AltFPSWeapon.weaponState.ToString());
                        TallyCombatSkills(currentmainHandItem);
                        return;
                    }
                }
            }
        }

        //controls off hand attack and ensures it can't be spammed/bugged.
        void OffhandAttack()
        {
            if(OffHandFPSWeapon.OffHandWeaponShow && OffHandFPSWeapon.weaponState == WeaponStates.Idle && !FPSShield.shieldEquipped && ParryState == 0)
            {
                //both weapons are idle, then perform attack routine....
                if (OffHandFPSWeapon.weaponState == WeaponStates.Idle && AltFPSWeapon.weaponState == WeaponStates.Idle && !FPSShield.shieldEquipped && ParryState == 0)
                {
                    //trigger offhand weapon attack animation routines.
                    attackState = randomattack[UnityEngine.Random.Range(0, randomattack.Length)];
                    OffHandFPSWeapon.weaponState = (WeaponStates)attackState;
                    GameManager.Instance.PlayerEntity.DecreaseFatigue(11);
                    StartCoroutine(OffHandFPSWeapon.AnimationCalculator());
                    OffHandFPSWeapon.PlaySwingSound();
                    Debug.Log(OffHandFPSWeapon.weaponState.ToString());
                    TallyCombatSkills(currentoffHandItem);
                    return;
                }
            }
        }

        //tallies skills when an attack is done based on the current tallyWeapon.
        void TallyCombatSkills(DaggerfallUnityItem tallyWeapon)
        {
            // Racial override can suppress optional attack voice
            RacialOverrideEffect racialOverride = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();
            bool suppressCombatVoices = racialOverride != null && racialOverride.SuppressOptionalCombatVoices;

            // Chance to play attack voice
            if (DaggerfallUnity.Settings.CombatVoices && !suppressCombatVoices && Dice100.SuccessRoll(20))
                GameManager.Instance.WeaponManager.ScreenWeapon.PlayAttackVoice();

            // Tally skills
            if (tallyWeapon == null)
            {
                GameManager.Instance.PlayerEntity.TallySkill(DFCareer.Skills.HandToHand, 1);
            }
            else
            {
                GameManager.Instance.PlayerEntity.TallySkill(tallyWeapon.GetWeaponSkillID(), 1);
            }

            GameManager.Instance.PlayerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);
        }

        //CONTROLS KEY INPUT TO ALLOW FOR NATURAL PARRY/ATTACK ANIMATIONS/REPONSES\\
        void KeyPressCheck()
        {
            if (Input.GetKeyDown(InputManager.Instance.GetBinding(InputManager.Actions.SwingWeapon)) || Input.GetKeyDown(offHandKeyCode))
            {
                attackKeyPressed = true;
            }

            if(attackKeyPressed)
            {
                timePass += Time.deltaTime;

                if (Input.GetKeyDown(InputManager.Instance.GetBinding(InputManager.Actions.SwingWeapon)))
                    playerInput.Enqueue(0);

                if (Input.GetKeyDown(offHandKeyCode))
                    playerInput.Enqueue(1);
            }               

            while (playerInput.Count > 0 && timePass > .16f)
            {
                attackKeyPressed = false;
                timePass = 0;

                if (playerInput.Contains(1) && playerInput.Contains(0))
                {
                    playerInput.Clear();
                    playerInput.Enqueue(2);
                }

                switch (playerInput.Dequeue())
                {
                    case 0:
                        //Debug.Log("Main Key Activated");
                        MainAttack();
                        break;
                    case 1:
                        //Debug.Log("Offhand Key Activated");
                        OffhandAttack();
                        break;
                    case 2:
                        //Debug.Log("Parry Activated");
                        Parry();
                        break;
                }

                playerInput.Clear();
            } 
        }

        //CHECKS PLAYERS ATTACK STATE USING BOTH HANDS.
        public static int checkAttackState()
        {
            if(AltFPSWeapon.weaponState != WeaponStates.Idle)
                attackState = (int)AltFPSWeapon.weaponState;
            if (AltFPSWeapon.weaponState != WeaponStates.Idle)
                attackState = (int)AltFPSWeapon.weaponState;
            if (AltFPSWeapon.isParrying || AltFPSWeapon.isParrying)
                attackState = 7;
            else
                attackState = 0;

            return attackState;
        }

        //CHECKS PLAYERS PARRY STATE USING BOTH HANDS:
        //need seperate from attackState() because parry state is a sub-state of idle state.
        //This is to deal with hijacking the old animation system and weapon states tied to it.
        public static int checkParryState()
        {
            //checks if both hands are idle. If so, sets mod/player attack state to idle/0.
            if (AltFPSWeapon.isParrying || OffHandFPSWeapon.isParrying)
                return 1;
            else
                return 0;
        }

        //Custom bow state block to maintain classic mechanics and mod compatibility.
        void BowState()
        {
            if (!arrowLoading && GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking() && GameManager.Instance.WeaponManager.ScreenWeapon.GetCurrentFrame() == 5)
            {
                cooldownTime = FormulaHelper.GetBowCooldownTime(GameManager.Instance.PlayerEntity);
                arrowLoading = true;
            }

            if(!arrowLoading)
            {   
                GameManager.Instance.WeaponManager.ScreenWeapon.ShowWeapon = true;
            }
            // Do nothing while weapon cooldown. Used for bow.

            if (arrowLoading)
            {
                Debug.Log(cooldownTime.ToString());
                cooldownTime -= Time.deltaTime;
                GameManager.Instance.WeaponManager.ScreenWeapon.ShowWeapon = false;

                if (cooldownTime <= 0f)
                {
                    arrowLoading = false;
                    GameManager.Instance.WeaponManager.ScreenWeapon.ChangeWeaponState(WeaponStates.Idle);
                }
                return;
            }

            if(!DaggerfallUnity.Settings.BowLeftHandWithSwitching && !GameManager.Instance.WeaponManager.UsingRightHand)
            {
                GameManager.Instance.WeaponManager.ScreenWeapon.ShowWeapon = false;

                //hide offhand weapon sprite, idle its state, and null out the equipped item.
                if(false)
                    OffHandFPSWeapon.OffHandWeaponShow = true;
                else
                    OffHandFPSWeapon.OffHandWeaponShow = false;

                OffHandFPSWeapon.equippedOffHandFPSWeapon = currentoffHandItem;

                if (currentoffHandItem != null)
                {
                    OffHandFPSWeapon.WeaponType = DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(currentoffHandItem);
                    OffHandFPSWeapon.MetalType = DaggerfallUnity.Instance.ItemHelper.ConvertItemMaterialToAPIMetalType(currentoffHandItem);
                    OffHandFPSWeapon.WeaponHands = ItemEquipTable.GetItemHands(currentoffHandItem);
                    OffHandFPSWeapon.SwingWeaponSound = currentoffHandItem.GetSwingSound();
                }

                OffHandFPSWeapon.currentFrame = 6;

                //hide main hand weapon sprite, idle its state, and null out the equipped item.
                AltFPSWeapon.AltFPSWeaponShow = true;

                FPSShield.equippedShield = null;
                FPSShield.shieldEquipped = false;
            }
            else
            {
                GameManager.Instance.WeaponManager.ScreenWeapon.ShowWeapon = true;
                //hide offhand weapon sprite, idle its state, and null out the equipped item.
                OffHandFPSWeapon.OffHandWeaponShow = false;
                AltFPSWeapon.weaponState = WeaponStates.Idle;

                //hide main hand weapon sprite, idle its state, and null out the equipped item.
                AltFPSWeapon.AltFPSWeaponShow = false;
                AltFPSWeapon.weaponState = WeaponStates.Idle;

                FPSShield.equippedShield = null;
                FPSShield.shieldEquipped = false;
            }              

            equipState = 6;
            return;
        }

        //checks players equipped hands and sets proper equipped states for associated script objects.
        void EquippedState()
        {
            //disable normal fps weapon by hiding it and minimizing its reach to 0 for safety.
            GameManager.Instance.WeaponManager.ScreenWeapon.ShowWeapon = false;
            GameManager.Instance.WeaponManager.ScreenWeapon.Reach = 0.0f;

            //if a bow is equipped, go to custom bow state and exit equipState().
            if ((currentmainHandItem != null && DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(currentmainHandItem) == WeaponTypes.Bow) || (currentoffHandItem != null && DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(currentoffHandItem) == WeaponTypes.Bow))
            {
                BowState();
                return;
            }

            //checks if main hand is equipped and sets proper object properties.
            if (currentmainHandItem != null)
            {
                AltFPSWeapon.AltFPSWeaponShow = true;
                //checks if the weapon is two handed, if so do....
                if (ItemEquipTable.GetItemHands(currentmainHandItem) == ItemHands.Both && !(DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(currentmainHandItem) == WeaponTypes.Melee))
                {
                    //set equip state to two handed.
                    equipState = 4;
                    //turn off offhand weapon rendering.
                    OffHandFPSWeapon.OffHandWeaponShow = false;
                    //null out offhand equipped weapon.
                    OffHandFPSWeapon.equippedOffHandFPSWeapon = null;
                    //
                    //turn off equipped shield.
                    FPSShield.equippedShield = null;
                    //null out equipped shield item.
                    FPSShield.shieldEquipped = false;
                    //return to ensure left hand routine isn't ran since using two handed weapon.
                    return;
                }
                //check if the equipped item is a shield
                else if (currentmainHandItem.IsShield)
                {
                    //settings equipped state to shield and main weapon
                    equipState = 3;
                    //don't render offhand weapon since shield is being rendered instead.
                    OffHandFPSWeapon.OffHandWeaponShow = false;
                    //runs and checks equipped shield and sets all proper triggers for shield module, including
                    //rendering and inputing management.
                    FPSShield.EquippedShield();
                }
                //sets equip state to 1. Check declaration for equipstate listing.
                equipState = 1;
            }
            //if right hand is empty do..
            else
            {
                //set to not equipped.
                equipState = 0;
                //set equipped item to nothing since using fist/melee now.
                AltFPSWeapon.equippedAltFPSWeapon = null;
                AltFPSWeapon.AltFPSWeaponShow = true;
            }

            //checks if main hand is equipped and sets proper object properties.
            if (currentoffHandItem != null)
            {
                //checks if the weapon is two handed, if so do....
                if (ItemEquipTable.GetItemHands(currentoffHandItem) == ItemHands.Both && !(DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(currentoffHandItem) == WeaponTypes.Melee))
                {
                    //set equip state to two handed.
                    equipState = 4;
                    //turn off offhand weapon rendering.
                    AltFPSWeapon.AltFPSWeaponShow = false;
                    //don't render off hand weapon.
                    OffHandFPSWeapon.OffHandWeaponShow = true;
                    //turn off equipped shield.
                    FPSShield.equippedShield = null;
                    //null out equipped shield item.
                    FPSShield.shieldEquipped = false;
                    //return to ensure left hand routine isn't ran since using two handed weapon.
                    return;
                }
                //check if the equipped item is a shield
                else if (currentoffHandItem.IsShield)
                {
                    //settings equipped state to shield and main weapon
                    equipState = 3;
                    //don't render offhand weapon since shield is being rendered instead.
                    OffHandFPSWeapon.OffHandWeaponShow = false;
                    //make offhand item null.
                    OffHandFPSWeapon.equippedOffHandFPSWeapon = null;
                    //runs and checks equipped shield and sets all proper triggers for shield module, including
                    //rendering and inputing management.
                    FPSShield.EquippedShield();
                }
                else
                {
                    FPSShield.equippedShield = null;
                    //null out equipped shield item.
                    FPSShield.shieldEquipped = false;

                   //if mainhand is equipped, set to duel wield. If not, set to main hand + melee state.
                    if (equipState == 1)
                        equipState = 5;
                    else
                        equipState = 2;

                    //render offhand weapon.
                    OffHandFPSWeapon.OffHandWeaponShow = true;
                }
            }
            //if offhand isn't equipped at all, turn of below object properties. Keep equip state 0 for nothing equipped.
            else
            {
                //offhand item is null.
                OffHandFPSWeapon.equippedOffHandFPSWeapon = null;
                //don't render off hand weapon.
                OffHandFPSWeapon.OffHandWeaponShow = true;
                //equipped shield item is null.
                FPSShield.equippedShield = null;
                //shield isn't equipped.
                FPSShield.shieldEquipped = false;
            }
        }

        //checks current player hands and the last equipped item. If either changed, update current equip state.
        //The equip states setup all the proper object properties for each script being controlled.
        void UpdateHands()
        {
            //if the weapons aren't swapping equipping then.
            if (EquipCountdownMainHand > 0 || EquipCountdownOffHand > 0)
            {
                //classic delay countdown timer. Stole and repurposed tons of weaponmanager.cs code.
                EquipCountdownMainHand -= Time.deltaTime * 980; // Approximating classic update time based off measuring video
                EquipCountdownOffHand -= Time.deltaTime * 980; // Approximating classic update time based off measuring video

                //inform player he has swapped their weapons. Replacement for old equipping weapon message.
                if (EquipCountdownMainHand < 0 && EquipCountdownOffHand < 0)
                {
                    EquipCountdownMainHand = 0;
                    EquipCountdownOffHand = 0;
                    DaggerfallUI.Instance.PopupMessage("Swapped Weapons");
                }   

                // Do nothing if weapon isn't done equipping
                if (EquipCountdownMainHand > 0 || EquipCountdownOffHand > 0)
                {
                    //disable vanilla render weapon.
                    GameManager.Instance.WeaponManager.ScreenWeapon.ShowWeapon = false;
                    //remove offhand weapon.
                    OffHandFPSWeapon.OffHandWeaponShow = false;
                    //remove altfps weapon.
                    AltFPSWeapon.AltFPSWeaponShow = false;
                    //remove shield render.
                    FPSShield.shieldEquipped = false;
                    return;
                }
            }

            if (AttackState == 0)
            {
                //checks if player has lefthandiness/flipped screen and they are using their main/right hand. Based on these two settings, it flips the weapon item hands to ensure proper animation alignments.
                if ((!GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal && GameManager.Instance.WeaponManager.UsingRightHand) || (GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal && !GameManager.Instance.WeaponManager.UsingRightHand))
                {
                    //normal assignment: right to right, left to left.
                    mainHandItem = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
                    offHandItem = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
                }
                else if ((!GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal && !GameManager.Instance.WeaponManager.UsingRightHand) || (GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal && GameManager.Instance.WeaponManager.UsingRightHand))
                {
                    //reverse assignment: right to left, left to right.
                    offHandItem = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
                    mainHandItem = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
                }
            }

            //Debug.Log((currentmainHandItem != null ? currentmainHandItem.ItemName.ToString() : "MELEE") + " & " + (currentoffHandItem != null ? currentoffHandItem.ItemName.ToString() : "MELEE") +" & " + (currentoffHandItem != null ? currentoffHandItem.GetItemHands().ToString() : "MELEE"));

            updateMainHand();
            updateOffHand();

            //checks the differing equipped hands and sets up properties to ensure proper onscreen rendering.
            EquippedState();
        }

        void updateOffHand()
        {
            // Left-hand item changed
            if (!DaggerfallUnityItem.CompareItems(currentoffHandItem, offHandItem))
            {
                if (GameManager.Instance.WeaponManager.UsingRightHand || !GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal)
                {
                    currentoffHandItem = weaponProhibited(offHandItem, currentoffHandItem);
                }
                else
                    currentoffHandItem = offHandItem;
            }

            if (currentoffHandItem == null)
            {
                if (GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope())
                {
                    OffHandFPSWeapon.WeaponType = WeaponTypes.Werecreature;
                    OffHandFPSWeapon.MetalType = MetalTypes.None;
                    OffHandFPSWeapon.SwingWeaponSound = SoundClips.SwingHighPitch;
                }
                else
                {
                    //sets up offhand render for melee combat/fist sprite render.
                    OffHandFPSWeapon.WeaponType = WeaponTypes.Melee;
                    OffHandFPSWeapon.MetalType = MetalTypes.None;
                }
            }
            else if (!currentoffHandItem.IsShield)
            {
                // Must be a weapon
                if (currentoffHandItem.ItemGroup != ItemGroups.Weapons)
                    return;

                // Sets up weapon objects for offhand fps script. This ensures the loadatlas works properly and updates the rendered sprite.
                OffHandFPSWeapon.WeaponType = DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(currentoffHandItem);
                OffHandFPSWeapon.MetalType = DaggerfallUnity.Instance.ItemHelper.ConvertItemMaterialToAPIMetalType(currentoffHandItem);
                OffHandFPSWeapon.WeaponHands = ItemEquipTable.GetItemHands(currentoffHandItem);
                OffHandFPSWeapon.SwingWeaponSound = offHandItem.GetSwingSound();
            }
            else
            {
                // Sets up shield object for FPSShield script. This ensures the equippedshield routine runs currectly.
                FPSShield.equippedShield = currentoffHandItem;
            }
        }

        void updateMainHand()
        {
            // Right-hand item changed
            if (!DaggerfallUnityItem.CompareItems(currentmainHandItem, mainHandItem))
            {
                if (!GameManager.Instance.WeaponManager.UsingRightHand || GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal)
                {
                    currentmainHandItem = weaponProhibited(mainHandItem, currentmainHandItem);
                }
                else
                    currentmainHandItem = mainHandItem;
            }

            if (currentmainHandItem == null)
            {
                if (GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope())
                {
                    AltFPSWeapon.WeaponType = WeaponTypes.Werecreature;
                    AltFPSWeapon.MetalType = MetalTypes.None;
                    GameManager.Instance.WeaponManager.ScreenWeapon.DrawWeaponSound = SoundClips.None;
                    GameManager.Instance.WeaponManager.ScreenWeapon.SwingWeaponSound = SoundClips.SwingHighPitch;
                }
                else
                {
                    //sets up offhand render for melee combat/fist sprite render.
                    AltFPSWeapon.WeaponType = WeaponTypes.Melee;
                    AltFPSWeapon.MetalType = MetalTypes.None;
                }
            }
            else if (!currentmainHandItem.IsShield)
            {
                // Must be a weapon
                if (currentmainHandItem.ItemGroup != ItemGroups.Weapons)
                    return;

                // Sets up weapon objects for replacement fps script. This ensures the loadatlas works properly and updates the rendered sprite.
                AltFPSWeapon.WeaponType = DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(currentmainHandItem);
                AltFPSWeapon.MetalType = DaggerfallUnity.Instance.ItemHelper.ConvertItemMaterialToAPIMetalType(currentmainHandItem);
                AltFPSWeapon.WeaponHands = ItemEquipTable.GetItemHands(currentmainHandItem);
                GameManager.Instance.WeaponManager.ScreenWeapon.DrawWeaponSound = mainHandItem.GetEquipSound();
                GameManager.Instance.WeaponManager.ScreenWeapon.SwingWeaponSound = mainHandItem.GetSwingSound();
            }
            else if (currentmainHandItem.IsShield)
            {
                AltFPSWeapon.WeaponType = DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(offHandItem);
                AltFPSWeapon.MetalType = DaggerfallUnity.Instance.ItemHelper.ConvertItemMaterialToAPIMetalType(offHandItem);
                AltFPSWeapon.WeaponHands = ItemEquipTable.GetItemHands(offHandItem);
                // Sets up shield object for FPSShield script. This ensures the equippedshield routine runs currectly.
                FPSShield.equippedShield = currentmainHandItem;
            }
        }

        DaggerfallUnityItem weaponProhibited(DaggerfallUnityItem checkedWeapon, DaggerfallUnityItem replacementWeapon = null)
        {
            if (checkedWeapon != null && prohibitedWeapons.Contains(checkedWeapon.ItemTemplate.index))
            {
                DaggerfallUI.Instance.PopupMessage("This weapon throws your balance off too much to use.");

                GameManager.Instance.PlayerEntity.ItemEquipTable.UnequipItem(checkedWeapon);

                 return null;
            }                
            else
                return checkedWeapon;
        }

        void ToggleHand()
        {
            if (DaggerfallUnity.Settings.BowLeftHandWithSwitching)
            {
                int switchDelay = 0;
                if (currentmainHandItem != null)
                    switchDelay += WeaponManager.EquipDelayTimes[mainHandItem.GroupIndex] - 500;
                if (currentoffHandItem != null)
                    switchDelay += WeaponManager.EquipDelayTimes[offHandItem.GroupIndex] - 500;
                if (switchDelay > 0)
                {
                    EquipCountdownMainHand += switchDelay / 1.7f;
                    EquipCountdownOffHand += switchDelay / 1.7f;
                    OffHandFPSWeapon.OffHandWeaponShow = false;
                    AltFPSWeapon.AltFPSWeaponShow = false;
                }
            }
        }

        //runs all the code for when two npcs parry each other. Uses calculateattackdamage formula to help it figure this out.
        public static void activateNPCParry(DaggerfallEntity targetEntity, DaggerfallEntity attackerEntity, int parriedDamage)
        {
            Destroy(Instantiate(sparkParticles, attackerEntity.EntityBehaviour.transform.position + (attackerEntity.EntityBehaviour.transform.forward * .35f), Quaternion.identity, null), 1.0f);
            //grab hit entity's motor component and assign it to targetMotor object.
            EnemyMotor targetMotor = targetEntity.EntityBehaviour.GetComponent<EnemyMotor>();
            //grab hit entity's motor component and assign it to targetMotor object.
            EnemyMotor attackMotor = attackerEntity.EntityBehaviour.GetComponent<EnemyMotor>();

            //finds daggerfall audio source object, loads it, and then adds it to the player object, so it knows where the sound source is from.
            DaggerfallAudioSource targetAudioSource = targetEntity.EntityBehaviour.GetComponent<DaggerfallAudioSource>();

            //finds daggerfall audio source object, loads it, and then adds it to the player object, so it knows where the sound source is from.
            DaggerfallAudioSource attackerAudioSource = targetEntity.EntityBehaviour.GetComponent<DaggerfallAudioSource>();

            //stole below code block/formula from enemyAttack script to calculate knockback amounts based on enemy weight and damage done.
            EnemyEntity enemyEntity = attackerEntity as EnemyEntity;
            float enemyWeight = enemyEntity.GetWeightInClassicUnits();
            float tenTimesDamage = parriedDamage * 10;
            float twoTimesDamage = parriedDamage * 2;

            float knockBackAmount = ((tenTimesDamage - enemyWeight) * 256) / (enemyWeight + tenTimesDamage) * twoTimesDamage;
            float KnockbackSpeed = (tenTimesDamage / enemyWeight) * (twoTimesDamage - (knockBackAmount / 256));
            KnockbackSpeed /= (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10);

            if (KnockbackSpeed < (15 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10)))
                KnockbackSpeed = (15 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10));

            //how far enemy will push back from the damaged ealt.
            targetMotor.KnockbackSpeed = KnockbackSpeed;
            attackMotor.KnockbackSpeed = KnockbackSpeed;
            //what direction they will go. Grab the players camera and push them the direction they are looking (aka away from player since they are looking forward).
            targetMotor.KnockbackDirection = -targetMotor.transform.forward;
            //what direction they will go. Grab the players camera and push them the direction they are looking (aka away from player since they are looking forward).
            attackMotor.KnockbackDirection = -attackMotor.transform.forward;
            //play random hit sound from the npc combatants.
            targetAudioSource.PlayOneShot(DFRandom.random_range_inclusive(108, 112), 1, 1);
            attackerAudioSource.PlayOneShot(DFRandom.random_range_inclusive(108, 112), 1, 1);
        }

        //runs all the code for when player and npc parry each other. Uses calculateattackdamage formula to help it figure this out.
        public static void activatePlayerParry(DaggerfallEntity attackerEntity, int parriedDamage)
        {
            Destroy(Instantiate(sparkParticles, attackerEntity.EntityBehaviour.transform.position + (attackerEntity.EntityBehaviour.transform.forward * .35f), Quaternion.identity, null), 1.0f);
            //grab hit entity's motor component and assign it to targetMotor object.
            EnemyMotor attackMotor = attackerEntity.EntityBehaviour.GetComponent<EnemyMotor>();
            //finds daggerfall audio source object, loads it, and then adds it to the player object, so it knows where the sound source is from.
            DaggerfallAudioSource dfAudioSource = GameManager.Instance.PlayerEntity.EntityBehaviour.GetComponent<DaggerfallAudioSource>();
            //how far enemy will push back from the damaged ealt.
            attackMotor.KnockbackSpeed = Mathf.Clamp(parriedDamage, 4f, 10f);
            //what direction they will go. Grab the players camera and push them the direction they are looking (aka away from player since they are looking forward).
            attackMotor.KnockbackDirection = -attackMotor.transform.forward;
            //uses playerentity object and attaches a character controller object to it for moving the player around.
            CharacterController playerController = GameManager.Instance.PlayerMotor.GetComponent<CharacterController>();
            //sets up the motion in a vector3 data point. Computes the data point by taking the parried damage and clamping it then multiplying it by the backward vector3 point.
            Vector3 motion = -playerController.transform.forward * Mathf.Clamp(parriedDamage, 8f, 14f); ;
            //moves player to vector3 data point.
            playerController.SimpleMove(motion);
            //play random hit sound from the player and attack voice to simulate parry happening in audio.
            dfAudioSource.PlayOneShot(DFRandom.random_range_inclusive(108, 112), 1, 1);
            GameManager.Instance.WeaponManager.ScreenWeapon.PlayAttackVoice();
        }

        public static bool AttackCast(DaggerfallUnityItem weapon, Vector3 attackcast, out GameObject attackHit)
        {
            bool hitObject = false;
            attackHit = null;
            //assigns the above triggered attackcast to the debug ray for easy debugging in unity.
            Debug.DrawRay(mainCamera.transform.position + (mainCamera.transform.forward * .25f), attackcast , Color.red, 5);
            //creates engine raycast, assigns current player camera position as starting vector and attackcast vector as the direction.
            RaycastHit hit;
            Ray ray = new Ray(mainCamera.transform.position, attackcast);

            //reverts to raycasts when physical weapon setting is turned on.
            //this ensures multiple sphere hits aren't registered on the same entity/object.
            if (!physicalWeapons)
                hitObject = Physics.SphereCast(ray, 0.25f, out hit, 2.5f, playerLayerMask);
            else
                hitObject = Physics.Raycast(ray, out hit, 2.5f, playerLayerMask);

            //if spherecast hits something, do....
            if (hitObject)
            {
                //checks if it hit a environment object. If not, begins enemy damage work.
                if (!GameManager.Instance.WeaponManager.WeaponEnvDamage(weapon, hit)
                    // Fall back to simple ray for narrow cages https://forums.dfworkshop.net/viewtopic.php?f=5&t=2195#p39524
                    || Physics.Raycast(ray, out hit, 2.5f, playerLayerMask))
                {
                    //grab hit entity properties for ues.
                    DaggerfallEntityBehaviour entityBehaviour = hit.transform.GetComponent<DaggerfallEntityBehaviour>();
                    EnemyAttack targetAttack = hit.transform.GetComponent<EnemyAttack>();
                    // Check if hit a mobile NPC
                    MobilePersonNPC mobileNpc = hit.transform.GetComponent<MobilePersonNPC>();

                    attackHit = hit.transform.gameObject;

                    //if attackable entity is hit, do....
                    if (entityBehaviour || mobileNpc)
                    {
                        if (GameManager.Instance.WeaponManager.WeaponDamage(weapon, false, hit.transform, hit.point, mainCamera.transform.forward))
                        {
                            hitObject = true;
                            //bashedEnemyEntity = entityBehaviour.Entity as EnemyEntity;
                            //DaggerfallEntity enemyEntity = entityBehaviour.Entity;
                            //DaggerfallMobileUnit entityMobileUnit = entityBehaviour.GetComponentInChildren<DaggerfallMobileUnit>();
                        }
                        //else, play high or low pitch swing miss randomly.
                        else
                        {
                            hitObject = false;
                            dfAudioSource.PlayOneShot(DFRandom.random_range_inclusive(105, 106), .5f, .5f);
                        }
                    }
                    //check if environment object is hit and do proper work.
                    else if (GameManager.Instance.WeaponManager.WeaponEnvDamage(weapon, hit))
                        hitObject = true;
                }
            }
            return hitObject;
        }
    }
}
