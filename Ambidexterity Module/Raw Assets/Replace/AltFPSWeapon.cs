using UnityEngine;
using DaggerfallWorkshop.Game.Items;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.IO;
using DaggerfallWorkshop.Utility;
using System.Collections.Generic;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallConnect.Utility;
using System;
using DaggerfallWorkshop.Game.Formulas;
using System.Collections;
using DaggerfallWorkshop.Game.Serialization;
using System.Diagnostics;
using System.Threading.Tasks;
using static DaggerfallWorkshop.Game.WeaponManager;

namespace AmbidexterityModule
{
    public class AltFPSWeapon : MonoBehaviour
    {
        public static AltFPSWeapon AltFPSWeaponInstance;

        //formula helper entities.
        public DaggerfallEntity targetEntity;
        public DaggerfallEntity attackerEntity;
        public static DaggerfallUnity dfUnity;
        public DaggerfallUnityItem equippedAltFPSWeapon;
        private static CifRciFile cifFile;
        public static Texture2D weaponAtlas;

        public static Rect[] weaponRects;
        public static RecordIndex[] weaponIndices;
        public static Rect weaponPosition;
        public static WeaponAnimation[] weaponAnims;
        private int selectedFrame;
        public static Rect curAnimRect;

        public static GameObject attackHit;

        public static int currentFrame = 0;    
        static int frameBeforeStepping = 0;
        const int nativeScreenWidth = 320;
        const int nativeScreenHeight = 200;
        int leftUnarmedAnimIndex = 0;
        int[] totalfames = new int[] { 0, 1, 2, 3, 4 };

        //public static Coroutine ParryCoroutine;
        public Task ParryCoroutine;
        public Task PrimerCoroutine;

        public bool AltFPSWeaponShow;
        public static bool flip;
        public bool isParrying;
        private bool lerpfinished;
        private bool breatheTrigger;
        private bool attackCasted;
        private bool hitObject;

        public static float weaponScaleX;
        public static float weaponScaleY;
        public static float offsetY;
        public static float offsetX;
        static float posi;
        public float totalAnimationTime;
        private float smoothingRange;
        public float weaponReach;
        public float AttackSpeedMod;
        public float AttackMoveMod;
        public float UnsheathedMoveMod;
        public float yModifier;
        public float yModifier1;
        public float yModifier2;
        public float yModifier3;
        public float yModifier4;
        public float xModifier;
        public float xModifier1;
        public float xModifier2;
        public float xModifier3;
        public float xModifier4;
        public float arcSpeed;
        public float arcModifier;
        public float hitStart = .3f;
        public float hitEnd = .75f;

        static float timeCovered;
        static float percentagetime;
        private float framepercentage;
        private float avgFrameRate;
        private float attackFrameTime;
        private float animTickTime;
        private float lerpRange;

        public WeaponTypes currentWeaponType;
        public MetalTypes currentMetalType;
        public WeaponStates weaponState = WeaponStates.Idle;
        public WeaponTypes WeaponType = WeaponTypes.None;
        public MetalTypes MetalType = MetalTypes.None;
        public ItemHands WeaponHands;

        readonly byte[] leftUnarmedAnims = { 0, 1, 2, 3, 4, 2, 1, 0 };

        public static readonly Dictionary<int, Texture2D> customTextures = new Dictionary<int, Texture2D>();
        public static Texture2D curCustomTexture;

        public static float bob = .1f;
        private static bool bobSwitch = true;
        Stopwatch AnimationTimer = new Stopwatch();
        private static float timePass;
        private static float frameTime;
        private float frametime;
        private int hitType;

        //*COMBAT OVERHAUL ADDITION*//
        //switch used to set custom offset distances for each weapon.
        //because each weapon has its own sprites, each one needs slight
        //adjustments to ensure sprites seem as seemless as possible in transition.
        private float GetAnimationOffset()
        {
            WeaponTypes weapon = currentWeaponType;
            switch (weapon)
            {
                case WeaponTypes.Battleaxe_Magic:
                case WeaponTypes.Battleaxe:
                    arcSpeed = 1.15f + arcModifier;
                    return .31f;
                case WeaponTypes.LongBlade_Magic:
                case WeaponTypes.LongBlade:
                    arcSpeed = 1f + arcModifier;
                    return .33f;
                case WeaponTypes.Warhammer_Magic:
                case WeaponTypes.Warhammer:
                    arcSpeed = .95f + arcModifier;
                    return .32f;
                case WeaponTypes.Staff_Magic:
                case WeaponTypes.Staff:
                    arcSpeed = .9f + arcModifier;
                    return .33f;
                case WeaponTypes.Flail:
                case WeaponTypes.Flail_Magic:
                    arcSpeed =.85f + arcModifier;
                    return .33f;
                case WeaponTypes.Werecreature:
                    arcSpeed = 1.25f + arcModifier;
                    return .085f;
                case WeaponTypes.Melee:
                    arcSpeed = 1.25f + arcModifier;
                    return .15f;
                default:
                    arcSpeed = 1f + arcModifier;
                    return .33f;
            }
        }

        public IEnumerator AnimationCalculator(float startX = 0, float startY = 0, float endX = 0, float endY = 0, bool breath = false, float triggerpoint = 1, float CustomTime = 0, float startTime = 0, bool natural = false, bool frameLock = false, bool raycast = true)
        {
            while (true)
            {
                float totalTime;

                //*COMBAT OVERHAUL ADDITION*//
                //calculates lerp values for each frame change. When the frame changes,
                //it grabs the current total animation time, amount of passed time, users fps,
                //and then uses them to calculate and set the lerp value to ensure proper animation
                //offsetting no matter users fps or attack speed.
                frameBeforeStepping = currentFrame;

                if (CustomTime != 0)
                    totalTime = CustomTime;
                else
                    totalTime = totalAnimationTime;

                //if there is a start time for the animation, then start the animation timer there.
                if (startTime != 0 && timeCovered == 0)
                    timeCovered = startTime * totalTime;

                if (!AmbidexterityManager.classicAnimations)
                {
                    if(hitObject)
                    {
                        frametime -= Time.deltaTime * 2;
                        timeCovered -= Time.deltaTime * 2;
                    }
                    else if (!breatheTrigger && !hitObject)
                    {
                        frametime += Time.deltaTime;
                        // Distance moved equals elapsed time times speed.
                        timeCovered += Time.deltaTime;
                    }
                    else if (breatheTrigger && !hitObject)
                    {
                        frametime -= Time.deltaTime;
                        // Distance moved equals elapsed time times speed.
                        timeCovered -= Time.deltaTime;
                    }
                }
                else
                {
                    if (!breatheTrigger)
                        // Distance moved equals elapsed time times speed.
                        timeCovered = timeCovered + (totalTime / 5);
                    else if (breatheTrigger)
                        // Distance moved equals elapsed time times speed.
                        timeCovered = timeCovered - (totalTime / 5);
                }                

                //how much time has passed in the animation
                percentagetime = (float)Math.Round(timeCovered / totalTime, 2);
                framepercentage = (float)Math.Round(frametime / attackFrameTime, 2);

                if (!frameLock)
                    currentFrame = Mathf.FloorToInt(percentagetime * 5);


                //breath trigger to allow lerp to breath naturally back and fourth.
                if (percentagetime >= triggerpoint && !breatheTrigger)
                    breatheTrigger = true;
                else if (percentagetime <= 0 && breatheTrigger)
                    breatheTrigger = false;

                if (percentagetime >= 1 || percentagetime <= 0 && !lerpfinished)
                {
                    lerpfinished = true;
                    ResetAnimation();
                    UpdateWeapon();
                    yield break;
                }
                else
                    lerpfinished = false;

                offsetX = Mathf.Lerp(startX, endX, percentagetime);
                offsetY = Mathf.Lerp(startY, endY, percentagetime);
                posi = Mathf.Lerp(0, smoothingRange, framepercentage);

                UnityEngine.Debug.Log(posi.ToString() + " | " + percentagetime.ToString() + " | " + currentFrame.ToString() + " | " + weaponState.ToString());

                if (raycast)
                {
                    if (currentFrame == 2 && !isParrying && !attackCasted && !AmbidexterityManager.physicalWeapons)
                    {
                        Vector3 attackCast = AmbidexterityManager.mainCamera.transform.forward * weaponReach;
                        AmbidexterityManager.AmbidexterityManagerInstance.AttackCast(equippedAltFPSWeapon, attackCast, out attackHit);
                        attackCasted = true;
                    }

                    if ((percentagetime > hitStart && percentagetime < hitEnd) && !hitObject && AmbidexterityManager.physicalWeapons && !isParrying)
                    {
                        Vector3 attackcast = AmbidexterityManager.mainCamera.transform.forward * weaponReach;
                        if (weaponState == WeaponStates.StrikeRight)
                            attackcast = ArcCastCalculator(new Vector3(0, -90, 0), new Vector3(0, 90, 0), percentagetime * arcSpeed, attackcast);
                        else if (weaponState == WeaponStates.StrikeDownRight)
                            attackcast = ArcCastCalculator(new Vector3(35, -35, 0), new Vector3(-30, 35, 0), percentagetime * arcSpeed, attackcast);
                        else if (weaponState == WeaponStates.StrikeLeft)
                            attackcast = ArcCastCalculator(new Vector3(0, 90, 0), new Vector3(0, -90, 0), percentagetime * arcSpeed, attackcast);
                        else if (weaponState == WeaponStates.StrikeDownLeft)
                            attackcast = ArcCastCalculator(new Vector3(35, 35, 0), new Vector3(0, -30, -35), percentagetime * arcSpeed, attackcast);
                        else if (weaponState == WeaponStates.StrikeDown)
                            attackcast = ArcCastCalculator(new Vector3(-45, 0, 0), new Vector3(45, 0, 0), percentagetime * arcSpeed, attackcast);
                        else if (weaponState == WeaponStates.StrikeUp)
                            attackcast = AmbidexterityManager.mainCamera.transform.forward * (Mathf.Lerp(0, weaponReach, percentagetime * arcSpeed));                        

                        if (AmbidexterityManager.AmbidexterityManagerInstance.AttackCast(equippedAltFPSWeapon, attackcast, out attackHit))
                        {
                            hitObject = AmbidexterityManager.AmbidexterityManagerInstance.AttackCast(equippedAltFPSWeapon, attackcast, out attackHit);
                        }
                    }
                }

                if (frameBeforeStepping != currentFrame)
                {
                    if(!hitObject)
                    {
                        posi = 0;
                        frametime = 0;
                    }
                    else
                    {
                        posi = smoothingRange;
                        frametime = attackFrameTime;
                    }                        
                }

                UpdateWeapon();

                if (!AmbidexterityManager.classicAnimations)
                    yield return new WaitForFixedUpdate();
                else
                    yield return new WaitForSecondsRealtime(totalTime / 5);

            }
        }

        //uses vector3 axis rotations to figure out starting and ending point of arc, then uses lerp to calculate where the ray is in the arc, and then returns the calculations.
        public Vector3 ArcCastCalculator(Vector3 startPosition, Vector3 endPosition, float percentageTime, Vector3 castDirection)
        {
            if (flip)
            {
                startPosition = startPosition * -1;
                endPosition = endPosition * -1;
            }

            //sets up starting and ending quaternion angles for the vector3 offset/raycast.
            Quaternion startq = Quaternion.Euler(startPosition);
            Quaternion endq = Quaternion.Euler(endPosition);
            //computes rotation for each raycast using a lerp. The time percentage is modified above using the animation time.
            Quaternion slerpq = Quaternion.Slerp(startq, endq, percentageTime);
            Vector3 attackcast = slerpq * castDirection;
            return attackcast;
        }

        public void ResetAnimation()
        {
            timeCovered = 0;
            currentFrame = 0;
            frametime = 0;
            isParrying = false;
            breatheTrigger = false;
            hitObject = false;
            attackCasted = false;
            weaponState = WeaponStates.Idle;
            AmbidexterityManager.AmbidexterityManagerInstance.AttackState = 0;
            AmbidexterityManager.AmbidexterityManagerInstance.isAttacking = false;
            GameManager.Instance.WeaponManager.ScreenWeapon.ChangeWeaponState(WeaponStates.Idle);
            AmbidexterityManager.isHit = false;
            posi = 0;
            offsetX = 0;
            offsetY = 0;
        }

        //draws gui shield.
        private void OnGUI()
        {
            GUI.depth = 1;
            //if shield is not equipped or console is open then....
            if (!AltFPSWeaponShow || GameManager.Instance.WeaponManager.Sheathed || AmbidexterityManager.consoleController.ui.isConsoleOpen || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return; //show nothing.            
            else
            {
                // Must have current weapon texture atlas
                if (weaponAtlas == null || WeaponType != currentWeaponType || MetalType != currentMetalType)
                {
                    ResetAnimation();
                    LoadWeaponAtlas();
                    UpdateWeapon();
                    if (weaponAtlas == null)
                        return;
                }

                if (Event.current.type.Equals(EventType.Repaint))
                {
                    // Draw weapon texture behind other HUD elements                    
                    GUI.DrawTextureWithTexCoords(weaponPosition, curCustomTexture ? curCustomTexture : weaponAtlas, curAnimRect);
                    UnityEngine.Debug.Log("Main: " + curCustomTexture + " | " + curCustomTexture.width + " | " + curCustomTexture.height + " | " + curAnimRect + " | " + weaponAtlas);
                }

                if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight) || InputManager.Instance.HasAction(InputManager.Actions.MoveLeft) || InputManager.Instance.HasAction(InputManager.Actions.MoveForwards) || InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards))
                {
                    UpdateWeapon();
                }
            }
        }

        public void UpdateWeapon()
        {
            int frameBeforeStepping = currentFrame;
            // Do nothing if weapon not ready
            if (weaponAtlas == null || weaponAnims == null ||
                weaponRects == null || weaponIndices == null)
            {
                return;
            }

            // Store rect and anim
            int weaponAnimRecordIndex;
            weaponAnimRecordIndex = weaponAnims[(int)weaponState].Record;

            WeaponAnimation anim = weaponAnims[(int)weaponState];
            try
            {
                //check to see if the texture is an imported texture for setup.
                bool isImported = customTextures.TryGetValue(MaterialReader.MakeTextureKey(0, (byte)weaponAnimRecordIndex, (byte)currentFrame), out curCustomTexture);
                //create a blank rect object to assign and manipulate weapon sprite properties with.
                Rect rect = new Rect();

                //checks if player is parying. and not hit. If so, keep in idle frame for animation cleanliness.
                if (isParrying && !AmbidexterityManager.isHit)
                {
                    weaponAnimRecordIndex = 0;
                    rect = weaponRects[weaponIndices[0].startIndex];
                }
                //if not load the current weapon sprite record using below properties.
                else
                    rect = weaponRects[weaponIndices[weaponAnimRecordIndex].startIndex + currentFrame];

                //flips the sprite rect so it matches the hand position. Without this, the image won't flip on left-handed option selected.
                if (flip)
                {
                    // Mirror weapon rect.
                    if (isImported)
                        curAnimRect = new Rect(1, 0, -1, 1);
                    else
                        curAnimRect = new Rect(rect.xMax, rect.yMin, -rect.width, rect.height);
                }
                //if not flip, assign the current animation rect object to the just loaded rect object for further use.
                else
                    curAnimRect = rect;

                if (isImported)
                    curAnimRect = new Rect(0, 0, 1, 1);

                if (weaponState == WeaponStates.StrikeDownLeft)
                    offsetX = -.09f;

                if (WeaponType == WeaponTypes.Werecreature)
                {
                    if (weaponState == WeaponStates.Idle)
                    {
                        weaponAnimRecordIndex = 5;
                        rect = weaponRects[weaponIndices[5].startIndex + 2];
                        offsetY = -.05f;
                        if (flip)
                        {
                            offsetX = .5f;
                            curAnimRect = rect;
                        }
                        else
                        {
                            offsetX = -.5f;
                            curAnimRect = new Rect(rect.xMax, rect.yMin, -rect.width, rect.height);
                        }
                    }
                    else if (weaponState == WeaponStates.StrikeDownRight)
                    {
                        if (!flip)
                        {
                            offsetX = .6f;
                            curAnimRect = new Rect(rect.xMax, rect.yMin, -rect.width, rect.height);
                        }
                        else
                            curAnimRect = rect;
                    }
                    else if (weaponState == WeaponStates.StrikeUp)
                    {
                        if (!flip)
                        {
                            offsetX = .6f;
                            curAnimRect = new Rect(rect.xMax, rect.yMin, -rect.width, rect.height);
                        }
                        else
                        {
                            curAnimRect = rect;
                        }

                    }
                }

                //*COMBAT OVERHAUL ADDITION*//
                //added offset checks for individual attacks and weapons. Also, allows for the weapon bobbing effect.
                //helps smooth out some animaitions by swapping out certain weapon animation attack frames and repositioning.
                //to line up the 5 animation frame changes with one another. This was critical for certain weapons and attacks.
                //this is a ridiculous if then loop set. Researching better ways of structuring this, of possible.
                if (AmbidexterityManager.AmbidexterityManagerInstance.AttackState == 0 && FPSShield.shieldStates == 0 && AmbidexterityManager.toggleBob && !AmbidexterityManager.classicAnimations)
                {
                    if (bob >= .10f && bobSwitch)
                        bobSwitch = false;
                    else if (bob <= 0 && !bobSwitch)
                        bobSwitch = true;

                    if (bobSwitch)
                        bob = bob + UnityEngine.Random.Range(.0005f, .001f);
                    else
                        bob = bob - UnityEngine.Random.Range(.0005f, .001f);

                    if (WeaponType == WeaponTypes.Werecreature)
                    {
                        weaponAnimRecordIndex = 5;
                        offsetX = (bob / 1.5f) - .57f;
                    }
                    else
                    {
                        weaponAnimRecordIndex = 0;
                        offsetX = (bob / 1.5f) - .07f;
                    }

                    offsetY = (bob * 1.5f) - .15f;
                }
                else if (!isParrying && weaponState != WeaponStates.Idle && !AmbidexterityManager.classicAnimations)
                {
                    //posi = posi * posiModifier;

                    if (weaponState == WeaponStates.StrikeLeft)
                    {
                        if (WeaponType == WeaponTypes.Flail || WeaponType == WeaponTypes.Flail_Magic)
                        {
                            if (currentFrame == 0)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 3;
                                offsetX = posi - .95f;
                            }
                            else if (currentFrame == 1)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 3;
                                offsetX = posi - .66f;
                            }
                            else
                            {
                                selectedFrame = currentFrame;
                                offsetX = posi;
                                offsetY = (posi / 2) * -1;
                            }
                        }
                        else if (WeaponType == WeaponTypes.Dagger || WeaponType == WeaponTypes.Dagger_Magic)
                        {
                            if (currentFrame == 0)
                            {
                                selectedFrame = 1;
                                weaponAnimRecordIndex = 2;
                                offsetX = posi - .55f;
                                offsetY = -.18f;
                            }
                            else if (currentFrame == 1)
                            {
                                selectedFrame = 2;
                                weaponAnimRecordIndex = 2;
                                offsetX = posi - .33f;
                            }
                            else
                            {
                                selectedFrame = currentFrame;
                                offsetX = posi;
                            }

                        }
                        else if (WeaponType == WeaponTypes.Melee)
                        {
                            selectedFrame = currentFrame;
                            weaponAnimRecordIndex = 2;
                            if (currentFrame <= 2)
                            {
                                offsetX = -.5f;
                                offsetY = posi - .165f;
                            }
                            else if (currentFrame == 3)
                            {
                                offsetX = -.5f;
                                offsetY = posi * -1;
                            }
                            else if (currentFrame == 4)
                            {
                                offsetX = -.5f;
                                offsetY = (posi * -1) - .165f;
                            }
                        }
                        else if (WeaponType == WeaponTypes.Staff)
                        {
                            selectedFrame = currentFrame;
                            offsetX = posi - .385f;
                        }
                        else if (WeaponType == WeaponTypes.LongBlade)
                        {
                            selectedFrame = currentFrame;
                            if (currentFrame == 0)
                                offsetX = posi - .33f;

                            if (currentFrame != 0)
                                offsetX = posi - .35f + (.1f * currentFrame);
                        }
                        else if (WeaponType == WeaponTypes.Werecreature)
                        {
                            selectedFrame = currentFrame;
                            offsetX = posi + .33f;
                        }
                        else
                        {
                            selectedFrame = currentFrame;
                            if (currentFrame == 0)
                                offsetX = posi - .33f;

                            if (currentFrame != 0)
                                offsetX = posi - .33f + (.11f * currentFrame);
                        }
                    }
                    else if (weaponState == WeaponStates.StrikeRight)
                    {
                        if (WeaponType == WeaponTypes.Flail || WeaponType == WeaponTypes.Flail_Magic)
                        {
                            if (currentFrame == 0)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 3;
                                offsetX = posi - .95f;
                            }
                            else if (currentFrame == 1)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 4;
                                offsetX = posi - .7f;
                            }
                            else
                            {
                                selectedFrame = currentFrame;
                                offsetX = posi;
                                offsetY = (posi / 2) - .1f;
                            }
                        }
                        else if (WeaponType == WeaponTypes.Dagger || WeaponType == WeaponTypes.Dagger_Magic)
                        {
                            selectedFrame = currentFrame;

                            if (currentFrame == 0)
                            {
                                offsetX = (posi / 4) * -1;
                                offsetY = (posi / 8) * -1;
                            }
                            else if (currentFrame == 1)
                            {
                                offsetX = ((posi / 4) * -1) - .0825f;
                                offsetY = ((posi / 8) * -1) - .04125f;
                            }
                            else
                            {
                                offsetX = posi - .125f;
                            }
                        }
                        else if (WeaponType == WeaponTypes.Melee)
                        {
                            selectedFrame = currentFrame;
                            if (currentFrame <= 1)
                            {
                                offsetX = posi - .15f;
                                offsetY = (posi / 2) - .15f;
                            }
                            else if (currentFrame == 2)
                            {
                                offsetX = posi - .45f;
                                offsetY = posi - .24f;
                            }
                            else if (currentFrame == 3)
                            {
                                offsetX = (posi - .45f);
                                offsetY = ((posi / 2) * -1);
                            }
                            else if (currentFrame == 4)
                            {
                                offsetX = (posi - .45f);
                                offsetY = ((posi / 2) * -1);
                            }
                        }
                        else if (WeaponType == WeaponTypes.Werecreature)
                        {
                            selectedFrame = currentFrame;
                            weaponAnimRecordIndex = 5;

                            offsetX = (posi * -1) + .3f;
                            offsetY = (posi / 3) * -1;                            
                        }
                        else if (WeaponType == WeaponTypes.Staff || WeaponType == WeaponTypes.Staff_Magic)
                        {
                            selectedFrame = currentFrame;

                            offsetX = (posi * -1.225f) + .4f;
                        }
                        else if (WeaponType == WeaponTypes.LongBlade)
                        {
                            selectedFrame = currentFrame;

                            if (currentFrame == 0)
                                offsetX = posi - .33f;

                            if (currentFrame != 0)
                                offsetX = posi - .35f + (.1f * currentFrame);
                        }
                        else
                        {
                            selectedFrame = currentFrame;

                            if (currentFrame == 0)
                                offsetX = posi - .33f;

                            if (currentFrame != 0)
                                offsetX = posi - .33f + (.11f * currentFrame);
                        }
                    }
                    else if (weaponState == WeaponStates.StrikeDown)
                    {
                        if (WeaponType == WeaponTypes.Flail || WeaponType == WeaponTypes.Flail_Magic)
                        {
                            if (currentFrame == 0)
                            {
                                selectedFrame = 2;
                                weaponAnimRecordIndex = 1;
                                offsetX = (posi) - .4f;
                                offsetY = ((posi / 2) * -1) + .1f;
                            }
                            else if (currentFrame == 1)
                            {
                                selectedFrame = 2;
                                weaponAnimRecordIndex = 2;
                                offsetX = (posi) - .28f;
                                offsetY = ((posi / 2) * -1) + .035f;
                            }
                            else if (currentFrame == 2)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 3) + .19f;
                                offsetY = (posi * -1) - .15f;
                            }
                            else if (currentFrame == 3)
                            {
                                selectedFrame = 2;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 3) + .2f;
                                offsetY = (posi * -1) - .275f;
                            }
                            else
                            {
                                selectedFrame = 1;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 3) + .2f;
                                offsetY = (posi * -1) - .375f;
                            }
                        }
                        else if (WeaponType == WeaponTypes.Dagger || WeaponType == WeaponTypes.Dagger_Magic)
                        {
                            selectedFrame = currentFrame;
                            if (currentFrame == 0)
                            {
                                offsetX = (posi / 2) - .33f;
                                offsetY = ((posi) * -1) + .05f;
                            }
                            else if (currentFrame == 1)
                            {
                                offsetX = (posi / 2) - .23f;
                                offsetY = ((posi) * -1) - .2f;
                            }
                            else
                            {
                                offsetX = (posi / 4) - .15f;
                                offsetY = (posi) * -1;
                            }
                        }
                        else if (WeaponType == WeaponTypes.Battleaxe || WeaponType == WeaponTypes.Battleaxe_Magic)
                        {
                            if (currentFrame == 0)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 6;

                                if (isImported)
                                {
                                    offsetX = (posi / 2) - .66f + xModifier;
                                    offsetY = (posi * -1f) + .71f + yModifier;
                                }
                                else
                                {
                                    offsetX = (posi / 2) - .45f + xModifier;
                                    offsetY = (posi * -1f) + .51f + yModifier;
                                }
                            }
                            else if (currentFrame == 1)
                            {
                                selectedFrame = 4;
                                weaponAnimRecordIndex = 6;

                                if (isImported)
                                {
                                    offsetX = (posi / 2) - .525f + xModifier1;
                                    offsetY = (posi * -1f) + .385f + yModifier1;
                                }
                                else
                                {
                                    offsetX = (posi / 2) - .3f + xModifier1;
                                    offsetY = (posi * -1f) + .11f + yModifier1;
                                }
                            }
                            else if (currentFrame == 2)
                            {
                                selectedFrame = 2;
                                weaponAnimRecordIndex = 1;

                                if (isImported)
                                {
                                    offsetX = (posi / 2) - .08f + xModifier2;
                                    offsetY = (posi * -1f) + .025f + yModifier2;
                                }
                                else
                                {
                                    offsetX = (posi / 2) + .07f + xModifier2;
                                    offsetY = (posi * -1f) - .11f + yModifier2;
                                }
                            }
                            else if (currentFrame == 3)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 1;

                                if (isImported)
                                {
                                    offsetX = (posi / 2) + .05f + xModifier3;
                                    offsetY = (posi * -1f) - .03f + yModifier3;
                                }
                                else
                                {
                                    offsetX = (posi / 2) + .2f + xModifier3;
                                    offsetY = (posi * -1f) - .23f + yModifier3;
                                }
                            }
                            else
                            {
                                selectedFrame = 4;
                                weaponAnimRecordIndex = 1;

                                if (isImported)
                                {
                                    offsetX = (posi / 2) + .2325f + xModifier4;
                                    offsetY = (posi * -1f) - .54f + yModifier4;
                                }
                                else
                                {
                                    offsetX = (posi / 2) + .3325f + xModifier4;
                                    offsetY = (posi * -1f) - .24f + yModifier4;
                                }
                            }
                        }
                        else if (WeaponType == WeaponTypes.Warhammer || WeaponType == WeaponTypes.Warhammer_Magic)
                        {
                            if (currentFrame == 0)
                            {
                                selectedFrame = 2;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 2) - .43f;
                                offsetY = (posi * -1f) + .53f;
                            }
                            else if (currentFrame == 1)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 2) - .27f;
                                offsetY = (posi * -1f) + .13f;
                            }
                            else if (currentFrame == 2)
                            {
                                selectedFrame = 4;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 2) - .175f;
                                offsetY = (posi * -1f) - .18f;
                            }
                            else if (currentFrame == 3)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 1;
                                offsetY = (posi * -1f) - .32f;
                                offsetX = (posi / 2) + .06f;
                            }
                            else
                            {
                                selectedFrame = 4;
                                weaponAnimRecordIndex = 1;
                                offsetX = (posi / 2) + .2725f;
                                offsetY = (posi * -1f) - .39f;
                            }
                        }
                        else if (WeaponType == WeaponTypes.Werecreature)
                        {
                            curAnimRect = isImported ? new Rect(0, 0, 1, 1) : weaponRects[weaponIndices[6].startIndex + currentFrame];
                            curAnimRect = new Rect(curAnimRect.xMax, curAnimRect.yMin, -curAnimRect.width, curAnimRect.height);
                            weaponAnimRecordIndex = 6;
                            if (currentFrame < 3)
                                offsetY = posi - .1f;
                            else
                                offsetY = (posi * -1);
                        }
                        else if (WeaponType == WeaponTypes.Melee)
                        {
                            curAnimRect = isImported ? new Rect(0, 0, 1, 1) : weaponRects[weaponIndices[3].startIndex + currentFrame];
                            curAnimRect = new Rect(curAnimRect.xMax, curAnimRect.yMin, -curAnimRect.width, curAnimRect.height);
                            weaponAnimRecordIndex = 3;
                            if (currentFrame < 4)
                                offsetY = posi - .14f;
                            else
                                offsetY = posi * -1.5f;
                        }
                        else if(WeaponType == WeaponTypes.Mace || WeaponType == WeaponTypes.Mace_Magic)
                        {
                            if (currentFrame == 0)
                            {
                                selectedFrame = 2;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 2) - .32f + xModifier;
                                offsetY = ((posi /2) * -1f) + .5f + yModifier;
                            }
                            else if (currentFrame == 1)
                            {
                                selectedFrame = 1;
                                weaponAnimRecordIndex = 4;
                                offsetX = (posi / 2) - .67f + xModifier1;
                                offsetY = ((posi / 2) * -1f) + .155f + yModifier1;
                            }
                            else if (currentFrame == 2)
                            {
                                selectedFrame = 4;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 2) - .065f + xModifier2;
                                offsetY = ((posi / 2) * -1f) + .05f + yModifier2;
                            }
                            else if (currentFrame == 3)
                            {
                                selectedFrame = currentFrame;
                                weaponAnimRecordIndex = 1;
                                offsetX = (posi / 3) + .085f + xModifier3;
                                offsetY = (posi * -1f) - .035f + yModifier3;
                            }
                            else if (currentFrame == 4)
                            {
                                selectedFrame = currentFrame;
                                weaponAnimRecordIndex = 1;
                                offsetX = (posi / 3) + .135f + xModifier4;
                                offsetY = (posi * -1f) - .135f + yModifier4;
                            }
                        }
                        else
                        {
                            if (currentFrame == 0)
                            {
                                selectedFrame = 4;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 2) - .33f + xModifier;
                                offsetY = (posi * -1f) + .3f + yModifier;
                            }
                            else if (currentFrame == 1)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 2) - .16f + xModifier1;
                                offsetY = (posi * -1f) + .05f + yModifier1;
                            }
                            else if (currentFrame == 2)
                            {
                                selectedFrame = 2;
                                weaponAnimRecordIndex = 6;
                                offsetX = (posi / 2) + xModifier2;
                                offsetY = (posi * -1f) - .15f + yModifier2;
                            }
                            else if (currentFrame == 3)
                            {
                                selectedFrame = 3;
                                weaponAnimRecordIndex = 1;
                                offsetX = (posi / 3) + .2f + xModifier3;
                                offsetY = (posi * -1f) - .17F + yModifier3;
                            }
                            else
                            {
                                selectedFrame = 4;
                                weaponAnimRecordIndex = 1;
                                offsetX = (posi / 3) + .225f + xModifier4;
                                offsetY = (posi * -1f) - .23F + yModifier4;
                            }
                        }
                    }
                    else if (weaponState == WeaponStates.StrikeUp)
                    {
                        if (WeaponType == WeaponTypes.Flail || WeaponType == WeaponTypes.Flail_Magic)
                        {
                            if (currentFrame == 0)
                            {
                                selectedFrame = currentFrame;
                                offsetY = (posi / 2) - .20f;
                            }
                            else if (currentFrame == 1)
                            {
                                selectedFrame = currentFrame;
                                offsetX = (posi / 7) * -1;
                                offsetY = (posi / 2) - .17f;
                            }
                            else if (currentFrame == 2)
                            {
                                selectedFrame = currentFrame;
                                offsetX = (posi / 6) * -1 - .05f;
                                offsetY = (posi / 2) - .13f;
                            }
                            if (currentFrame == 3)
                            {
                                selectedFrame = currentFrame;
                                offsetX = (posi / 5) * -1 - .12f;
                                offsetY = (posi / 2) - .07f;
                            }
                            else if (currentFrame == 4)
                            {
                                selectedFrame = 2;
                                weaponAnimRecordIndex = 1;
                                offsetX = (posi / 4) * -1 - .12f;
                                offsetY = (posi / 3) - .105f;
                            }
                        }
                        else if (WeaponType == WeaponTypes.Werecreature)
                        {
                            selectedFrame = currentFrame;
                            weaponAnimRecordIndex = 1;
                            if (currentFrame < 3)
                                offsetY = posi - .1f;
                            else
                                offsetY = (posi * -1);
                        }
                        else if (WeaponType == WeaponTypes.Staff || WeaponType == WeaponTypes.Staff_Magic)
                        {
                            selectedFrame = currentFrame;
                            if (currentFrame == 0)
                            {
                                offsetY = (posi * -1) * 2f;
                            }
                            else if (currentFrame == 1)
                            {
                                offsetY = (posi * .5f) - .7f;
                            }
                            else if (currentFrame == 2)
                            {
                                offsetY = (posi * .5f) - .58f;
                            }
                            else if (currentFrame == 3)
                            {
                                offsetY = (posi * .75f) - .5f;
                            }
                            else if (currentFrame == 4)
                            {
                                offsetY = (posi * .75f) - .25f;
                            }
                        }
                        else
                        {
                            selectedFrame = currentFrame;
                            if (currentFrame == 0)
                            {
                                offsetY = (posi * -1) * 2f;
                            }
                            else if (currentFrame == 1)
                            {
                                weaponAnimRecordIndex = 6;
                                offsetY = posi - 1f;
                            }
                            else if (currentFrame == 2)
                            {
                                weaponAnimRecordIndex = 6;
                                offsetY = posi - .816f;
                            }
                            else if (currentFrame == 3)
                            {
                                offsetY = posi - .614f;
                            }
                            else if (currentFrame == 4)
                            {
                                offsetY = posi - .312f;
                            }
                        }
                    }
                    else if (weaponState == WeaponStates.StrikeDownRight)
                    {
                        selectedFrame = currentFrame;
                        if (currentFrame < 3)
                        {
                            offsetX = posi / 2f;
                            offsetY = posi - .14f;
                        }
                        else
                        {
                            offsetX = posi;
                            offsetY = posi * -2;
                        }                           
                    }
                }

                if (isImported)
                {
                    curAnimRect.Set(0, 0, 1, 1);
                    customTextures.TryGetValue(MaterialReader.MakeTextureKey(0, (byte)weaponAnimRecordIndex, (byte)selectedFrame), out curCustomTexture);
                }
                else
                {
                    curAnimRect = weaponRects[weaponIndices[weaponAnimRecordIndex].startIndex + selectedFrame];
                }

                // Get weapon dimensions
                int width = weaponIndices[weaponAnimRecordIndex].width;
                int height = weaponIndices[weaponAnimRecordIndex].height;

                // Get weapon scale
                weaponScaleX = (float)Screen.width / (float)nativeScreenWidth;
                weaponScaleY = (float)Screen.height / (float)nativeScreenHeight;

                // Adjust scale to be slightly larger when not using point filtering
                // This reduces the effect of filter shrink at edge of display
                if (dfUnity.MaterialReader.MainFilterMode != FilterMode.Point)
                {
                    weaponScaleX *= 1.01f;
                    weaponScaleY *= 1.01f;
                }

                // Source weapon images are designed to overlay a fixed 320x200 display.
                // Some weapons need to align with both top, bottom, and right of display.
                // This means they might be a little stretched on widescreen displays.
                switch (anim.Alignment)
                {
                    case WeaponAlignment.Left:
                        AlignLeft(anim, width, height);
                        break;

                    case WeaponAlignment.Center:
                        AlignCenter(anim, width, height);
                        break;

                    case WeaponAlignment.Right:
                        AlignRight(anim, width, height);
                        break;
                }
            }
            catch (IndexOutOfRangeException)
            {
                DaggerfallUnity.LogMessage("Index out of range exception for weapon animation. Probably due to weapon breaking + being unequipped during animation.");
            }
        }

        private void AlignLeft(WeaponAnimation anim, int width, int height)
        {
            weaponPosition = new Rect(
                Screen.width * offsetX,
                (Screen.height - height * weaponScaleY) * (1f - offsetY),
                width * weaponScaleX,
                height * weaponScaleY);
        }

        private void AlignCenter(WeaponAnimation anim, int width, int height)
        {
            weaponPosition = new Rect(
                (((Screen.width * (1f - offsetX)) / 2f) - (width * weaponScaleX) / 2f),
                Screen.height * (1f - offsetY) - height * weaponScaleY,
                width * weaponScaleX,
                height * weaponScaleY);
        }

        private void AlignRight(WeaponAnimation anim, int width, int height)
        {
            if (flip)
            {
                // Flip alignment
                AlignLeft(anim, width, height);
                return;
            }

            weaponPosition = new Rect(
                Screen.width * (1f - offsetX) - width * weaponScaleX,
                (Screen.height * (1f - offsetY) - height * weaponScaleY),
                width * weaponScaleX,
                height * weaponScaleY);
        }

        public void LoadWeaponAtlas()
        {
            string filename = WeaponBasics.GetWeaponFilename(WeaponType);

            // Load the weapon texture atlas
            // Texture is dilated into a transparent coloured border to remove dark edges when filtered
            // Important to use returned UV rects when drawing to get right dimensions
            weaponAtlas = GetWeaponTextureAtlas(filename, MetalType, out weaponRects, out weaponIndices, 2, 2, true);
            weaponAtlas.filterMode = DaggerfallUnity.Instance.MaterialReader.MainFilterMode;

            // Get weapon anims
            weaponAnims = (WeaponAnimation[])WeaponBasics.GetWeaponAnims(WeaponType).Clone();

            // Store current weapon
            currentWeaponType = WeaponType;
            currentMetalType = MetalType;
            attackFrameTime = FormulaHelper.GetMeleeWeaponAnimTime(GameManager.Instance.PlayerEntity, WeaponType, WeaponHands) * AttackSpeedMod;
            totalAnimationTime = attackFrameTime * 5;

            smoothingRange = GetAnimationOffset();
        }

        #region Texture Loading

        private Texture2D GetWeaponTextureAtlas(
                string filename,
                MetalTypes metalType,
                out Rect[] rectsOut,
                out RecordIndex[] indicesOut,
                int padding,
                int border,
                bool dilate = false)
        {
            cifFile = new CifRciFile();
            cifFile.Palette.Load(Path.Combine(dfUnity.Arena2Path, cifFile.PaletteName));

            cifFile.Load(Path.Combine(dfUnity.Arena2Path, filename), FileUsage.UseMemory, true);

            // Read every image in archive
            Rect rect;
            List<Texture2D> textures = new List<Texture2D>();
            List<RecordIndex> indices = new List<RecordIndex>();
            customTextures.Clear();
            for (int record = 0; record < cifFile.RecordCount; record++)
            {
                int frames = cifFile.GetFrameCount(record);
                DFSize size = cifFile.GetSize(record);
                RecordIndex ri = new RecordIndex()
                {
                    startIndex = textures.Count,
                    frameCount = frames,
                    width = size.Width,
                    height = size.Height,
                };
                indices.Add(ri);
                for (int frame = 0; frame < frames; frame++)
                {
                    textures.Add(GetWeaponTexture2D(filename, record, frame, metalType, out rect, border, dilate));

                    Texture2D tex;
                    if (TextureReplacement.TryImportCifRci(filename, record, frame, metalType, true, out tex))
                    {
                        tex.filterMode = dfUnity.MaterialReader.MainFilterMode;
                        tex.wrapMode = TextureWrapMode.Mirror;
                        customTextures.Add(MaterialReader.MakeTextureKey(0, (byte)record, (byte)frame), tex);
                    }
                }
            }

            // Pack textures into atlas
            Texture2D atlas = new Texture2D(2048, 2048, TextureFormat.ARGB32, false);
            rectsOut = atlas.PackTextures(textures.ToArray(), padding, 2048);
            indicesOut = indices.ToArray();

            // Shrink UV rect to compensate for internal border
            float ru = 1f / atlas.width;
            float rv = 1f / atlas.height;
            for (int i = 0; i < rectsOut.Length; i++)
            {
                Rect rct = rectsOut[i];
                rct.xMin += border * ru;
                rct.xMax -= border * ru;
                rct.yMin += border * rv;
                rct.yMax -= border * rv;
                rectsOut[i] = rct;
            }

            return atlas;
        }

        private Texture2D GetWeaponTexture2D(
            string filename,
            int record,
            int frame,
            MetalTypes metalType,
            out Rect rectOut,
            int border = 0,
            bool dilate = false)
        {
            // Get source bitmap
            DFBitmap dfBitmap = cifFile.GetDFBitmap(record, frame);

            // Tint based on metal type
            // But not for steel as that is default colour in files
            if (metalType != MetalTypes.Steel && metalType != MetalTypes.None)
                dfBitmap = ImageProcessing.ChangeDye(dfBitmap, ImageProcessing.GetMetalDyeColor(metalType), DyeTargets.WeaponsAndArmor);

            // Get Color32 array
            DFSize sz;
            Color32[] colors = cifFile.GetColor32(dfBitmap, 0, border, out sz);

            // Dilate edges
            if (border > 0 && dilate)
                ImageProcessing.DilateColors(ref colors, sz);

            // Create Texture2D
            Texture2D texture = new Texture2D(sz.Width, sz.Height, TextureFormat.ARGB32, false);
            texture.SetPixels32(colors);
            texture.Apply(true);

            // Shrink UV rect to compensate for internal border
            float ru = 1f / sz.Width;
            float rv = 1f / sz.Height;
            rectOut = new Rect(border * ru, border * rv, (sz.Width - border * 2) * ru, (sz.Height - border * 2) * rv);

            return texture;
        }

        #endregion

        public WeaponStates OnAttackDirection(MouseDirections direction)
        {
            // Get state based on attack direction
            //WeaponStates state;

            switch (direction)
            {
                case MouseDirections.Down:
                    return WeaponStates.StrikeDown;
                case MouseDirections.DownLeft:
                    return WeaponStates.StrikeDownLeft;
                case MouseDirections.Left:
                    return WeaponStates.StrikeLeft;
                case MouseDirections.Right:
                    return WeaponStates.StrikeRight;
                case MouseDirections.DownRight:
                    return WeaponStates.StrikeDownRight;
                case MouseDirections.Up:
                    return WeaponStates.StrikeUp;
                default:
                    return WeaponStates.Idle;
            }
        }

    }
}
