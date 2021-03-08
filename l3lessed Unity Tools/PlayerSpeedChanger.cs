// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2020 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Interkarma
// Contributors:    Hazelnut, Allofich, Meteoric Dragon, jefetienne
// 
// Notes:
//

using DaggerfallConnect;
using System.Collections.Generic;
using UnityEngine;

namespace DaggerfallWorkshop.Game
{
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerSpeedChanger : MonoBehaviour
    {
        private PlayerMotor playerMotor;
        private LevitateMotor levitateMotor;

        // If checked, the run key toggles between running and walking. Otherwise player runs if the key is held down and walks otherwise
        // There must be a button set up in the Input Manager called "Run"
        public bool ToggleRun { get; set; }
        public bool ToggleSneak { get; set; }

        // Daggerfall base speed constants. (courtesy Allofich)
        public const float classicToUnitySpeedUnitRatio = 39.5f; // was estimated from comparing a walk over the same distance in classic and DF Unity
        public const float dfWalkBase = 150f;
        private const float dfCrouchBase = 50f;
        private const float dfRideBase = dfWalkBase + 225f;
        private const float dfCartBase = dfWalkBase + 100f;

        public bool walkSpeedOverride = true;
        public float currentWalkSpeed { get; private set; } = 0;
        private Dictionary<string, float> walkSpeedModifierList = new Dictionary<string, float>();

        public bool runSpeedOverride = true;
        public float currentRunSpeed { get; private set; } = 0;
        private Dictionary<string, float> runSpeedModifierList = new Dictionary<string, float>();

        public delegate bool CanPlayerRun();
        public CanPlayerRun CanRun { get; set; }
        public bool runningMode = false;
        public bool sneakingMode = false;

        public bool isRunning = false;
        public bool isSneaking = false;

        public bool updateWalkSpeed;
        public bool updateRunSpeed;

        private void Start()
        {
            playerMotor = GameManager.Instance.PlayerMotor;
            levitateMotor = GetComponent<LevitateMotor>();
            CanRun = CanRunUnlessRiding;
            currentWalkSpeed = GetWalkSpeed(GameManager.Instance.PlayerEntity);
            currentRunSpeed = GetRunSpeed();
        }



        /// <summary>
        /// Record player input for speed adjustment
        /// </summary>
        public void CaptureInputSpeedAdjustment()
        {
            if (!ToggleRun)
                runningMode = InputManager.Instance.HasAction(InputManager.Actions.Run);
            else
                runningMode = runningMode ^ InputManager.Instance.ActionStarted(InputManager.Actions.Run);

            if (!ToggleSneak)
                sneakingMode = InputManager.Instance.HasAction(InputManager.Actions.Sneak);
            else
                sneakingMode = sneakingMode ^ InputManager.Instance.ActionStarted(InputManager.Actions.Sneak);

            if (InputManager.Instance.ActionStarted(InputManager.Actions.AutoRun))
            {
                InputManager.Instance.ToggleAutorun = !InputManager.Instance.ToggleAutorun;

                ToggleRun = InputManager.Instance.ToggleAutorun;
                runningMode = runningMode ^ InputManager.Instance.ToggleAutorun;
            }

            if (InputManager.Instance.ActionStarted(InputManager.Actions.MoveBackwards))
            {
                ToggleRun = false;
            }
        }

        /// <summary>
        /// Determines how speed should be changed based on player's input
        /// </summary>
        /// <param name="speed"></param>
        public void ApplyInputSpeedAdjustment(ref float speed)
        {
            if (playerMotor.IsGrounded)
            {
                isRunning = CanRun() && runningMode;
                isSneaking = !isRunning && sneakingMode;
            }
            else
            {
                if (!CanRun())
                    isRunning = false;
                // you can't switch running on/off while in mid air
            }

            if (isRunning)
            {
                speed = RefreshRunspeed();

                //switch sneaking off if was previously sneaking
                sneakingMode = false;
            }
            else if (isSneaking)
            {
                // Handle sneak key. Reduces movement speed to half, then subtracts 1 in classic speed units
                speed /= 2;
                speed -= (1 / classicToUnitySpeedUnitRatio);
            }

            InputManager.Instance.MaximizeJoystickMovement = isRunning;
        }

        public bool CanRunUnlessRiding()
        {
            return !playerMotor.IsRiding;
        }


        /// <summary>
        /// Get LiveSpeed adjusted for swimming, walking, crouching or riding
        /// </summary>
        /// <returns>Speed based on player.Stats.LiveSpeed</returns>
        public float GetBaseSpeed()
        {
            Entity.PlayerEntity player = GameManager.Instance.PlayerEntity;
            float baseSpeed = 0;
            float playerSpeed = player.Stats.LiveSpeed;
            if (playerMotor == null) // fixes null reference bug.
                playerMotor = GameManager.Instance.PlayerMotor;
            // crouching speed penalty doesn't apply if swimming.
            if (playerMotor.IsCrouching && !levitateMotor.IsSwimming)
                baseSpeed = (playerSpeed + dfCrouchBase) / classicToUnitySpeedUnitRatio;
            else if (playerMotor.IsRiding)
            {
                float rideSpeed = (GameManager.Instance.TransportManager.TransportMode == TransportModes.Cart) ? dfCartBase : dfRideBase;
                baseSpeed = (playerSpeed + rideSpeed) / classicToUnitySpeedUnitRatio;
            }
            else
            {
                baseSpeed = RefreshWalkSpeed();
            }
            return baseSpeed;
        }

        /// <summary>
        /// Add custom walk speed modifier to speed modifer dictionary. Returns unique ID for referencing of custom speedModifier for future manipulation.
        /// </summary>
        /// <param name="speedModifier">the amount to change players base walk speed by percentages. AKA, .75 will lower player movement by 25%. Using 0 or negatives will do nothing but return null.
        /// <param name="refreshWalkSpeed">will cause routine to also update the player speed using the list to sequentially multiply the current base value by the list modifier values.</param>
        /// <returns></returns>        
        public string AddWalkSpeedMod(float walkSpeedModifier = 0, bool refreshWalkSpeed = true)
        {
            string UUID = null;

            //if they set a speed modifier greater than 0, grab the list index using count, and add item (which will be at the lastID index spot).
            if (walkSpeedModifier > 0)
            {
                UUID = System.Guid.NewGuid().ToString();
                walkSpeedModifierList.Add(UUID, walkSpeedModifier);
            }
            //trigger an update to the walk speed loop to push updated walk speed value.
            updateWalkSpeed = refreshWalkSpeed;

            return UUID;
        }

        /// <summary>
        /// Add custom walk speed modifier to speed modifer dictionary. Returns unique ID for referencing of custom speedModifier for future manipulation.
        /// </summary>
        /// <param name="speedModifier">the amount to change players base walk speed by percentages. AKA, .75 will lower player movement by 25%. Using 0 or negatives will do nothing but return null.
        /// <param name="refreshWalkSpeed">will cause routine to also update the player speed using the list to sequentially multiply the current base value by the list modifier values.</param>
        /// <returns></returns>        
        public string AddRunSpeedMod(float speedModifier = 0, bool refreshRunSpeed = true)
        {
            string UUID = null;

            //if they set a speed modifier greater than 0, grab the list index using count, and add item (which will be at the lastID index spot).
            if (speedModifier > 0)
            {
                UUID = System.Guid.NewGuid().ToString();
                runSpeedModifierList.Add(UUID, speedModifier);
            }

            //trigger an update to the walk speed loop to push updated walk speed value.
            updateRunSpeed = refreshRunSpeed;

            return UUID;
        }

        /// <summary>
        /// remove custom walk speed modifier from speed modifer dictionary using stored UID. Returns true if removed, false if not found.
        /// </summary>
        /// <param name="UUID">The Unique Universal ID created and provided when original value was added to dictionary.
        /// <param name="refreshWalkSpeed">will cause routine to also update the player speed using the list to sequentially multiply the current base value by the list modifier values.</param>
        /// <returns></returns>   
        public bool RemoveSpeedMod(string UUID, bool removeRunSpeed = false, bool refresSpeed = true)
        {
            //setup false bool for manipulation.
            bool removed = false;

            //if there is no uuid put in, return false as error catching.
            if (UUID == "" || UUID == null)
                return removed;

            //if there is a modifier put in, see if dictionary contains it in the unique keys, and then remove and return true.
            if (!removeRunSpeed && walkSpeedModifierList.ContainsKey(UUID))
            {
                walkSpeedModifierList.Remove(UUID);
                removed = true;
                Debug.Log("removed: " + UUID);
            }

            //if there is a modifier put in, see if dictionary contains it in the unique keys, and then remove and return true.
            if (removeRunSpeed && runSpeedModifierList.ContainsKey(UUID))
            {
                runSpeedModifierList.Remove(UUID);
                removed = true;
                Debug.Log("removed: " + UUID);
            }

            //trigger an update to the walk speed loop to push updated walk speed value.
            updateWalkSpeed = refresSpeed;
            updateRunSpeed = refresSpeed;

            return removed;
        }

        public bool ResetSpeed(bool walkSpeedReset = true, bool runSpeedReset = true)
        {
            bool reset = false;

            if(walkSpeedReset)
            {
                walkSpeedModifierList.Clear();
                reset = true;
                updateWalkSpeed = true;
            }                

            if (runSpeedReset)
            {
                runSpeedModifierList.Clear();
                reset = true;
                updateRunSpeed = true;
            }

            updateRunSpeed = true;
            return reset;
        }

        /// <summary>
        /// Updates the players walk speed using for loop and dictionary values to ensure proper sequential processing to get proper end speed.
        /// Processing of modifiers is processed by their addition order. First added by modder is multiplied first, and so on.
        /// </summary>
        private float RefreshWalkSpeed()
        {
            //setup and grab needed base values for computing end speed.
            float baseWalkSpeed = GetWalkSpeed(GameManager.Instance.PlayerEntity);
            float overrideSpeed = 0;

            //if there are no modifiers in the dictionary, return the base walk speed.
            if(walkSpeedModifierList.Count == 0 || walkSpeedOverride == false)
                return baseWalkSpeed;

            //if updateWalkSpeed switch is turned to true, update walk speed using an if then and while loop.
            if(updateWalkSpeed)
            {
                //shunt collection as a numerator into a object var to be used.
                using (var modifierValue = walkSpeedModifierList.GetEnumerator())
                {
                    //if the first item is moved do this, calculate speed using base speed as the starting point.
                    if (modifierValue.MoveNext())
                    {
                        overrideSpeed = baseWalkSpeed * modifierValue.Current.Value;

                        //once first move next is done to get speed from base value, start while loop to sequentally calculate subsequent values.
                        while (modifierValue.MoveNext())
                        {
                            Debug.Log(modifierValue.Current.Key.ToString() + " Modified: " + overrideSpeed);
                            overrideSpeed = overrideSpeed * modifierValue.Current.Value;
                        }
                    }
                }

                //assign override speed and switch updateWalkSpeed to false to stop it from looping every time it is called.
                //only want it to loop when collection changes - AKA, add, remove, ect - or modder forces switch flip.
                currentWalkSpeed = overrideSpeed;
                updateWalkSpeed = false;
            }

            //return the final modified walk speed.
            return currentWalkSpeed;
        }

        /// <summary>
        /// Updates the players walk speed using for loop and dictionary values to ensure proper sequential processing to get proper end speed.
        /// Processing of modifiers is processed by their addition order. First added by modder is multiplied first, and so on.
        /// </summary>
        private float RefreshRunspeed()
        {
            //setup and grab needed base values for computing end speed.
            float baseRunSpeed = GetRunSpeed();
            float overrideSpeed = 0;

            //if there are no modifiers in the dictionary, return the base walk speed.
            if (runSpeedModifierList.Count == 0 || runSpeedOverride == false)
                return baseRunSpeed;

            //if updateWalkSpeed switch is turned to true, update walk speed using an if then and while loop.
            if (updateRunSpeed)
            {
                //shunt collection as a numerator into a object var to be used.
                using (var modifierValue = runSpeedModifierList.GetEnumerator())
                {
                    //if the first item is moved do this, calculate speed using base speed as the starting point.
                    if (modifierValue.MoveNext())
                    {
                        overrideSpeed = baseRunSpeed * modifierValue.Current.Value;

                        //once first move next is done to get speed from base value, start while loop to sequentally calculate subsequent values.
                        while (modifierValue.MoveNext())
                        {
                            Debug.Log(modifierValue.Current.Key.ToString() + " Modified: " + overrideSpeed);
                            overrideSpeed = overrideSpeed * modifierValue.Current.Value;
                        }
                    }
                }

                //assign override speed and switch updateWalkSpeed to false to stop it from looping every time it is called.
                //only want it to loop when collection changes - AKA, add, remove, ect - or modder forces switch flip.
                currentRunSpeed = overrideSpeed;
                updateRunSpeed = false;
            }

            //return the final modified walk speed.
            return currentRunSpeed;
        }

        /// <summary>
        /// Get LiveSpeed adjusted for walking
        /// </summary>
        /// <param name="player">the PlayerEntity to use</param>
        /// <returns></returns>
        public float GetWalkSpeed(Entity.PlayerEntity player)
        {
            float drag = 0.5f * (100 - (player.Stats.LiveSpeed >= 30 ? player.Stats.LiveSpeed : 30));
            return (player.Stats.LiveSpeed + dfWalkBase - drag) / classicToUnitySpeedUnitRatio;
        }

        /// <summary>
        /// Get LiveSpeed adjusted for running
        /// </summary>
        /// <param name="baseSpeed"></param>
        /// <returns></returns>
        public float GetRunSpeed()
        {
            Entity.PlayerEntity player = GameManager.Instance.PlayerEntity;
            float baseRunSpeed = (player.Stats.LiveSpeed + dfWalkBase) / classicToUnitySpeedUnitRatio;
            return baseRunSpeed * (1.35f + (player.Skills.GetLiveSkillValue(DFCareer.Skills.Running) / 200f));
        }

        /// <summary>
        /// Get LiveSpeed adjusted for swimming
        /// </summary>
        /// <param name="baseSpeed"></param>
        /// <returns></returns>
        public float GetSwimSpeed(float baseSpeed)
        {
            Entity.PlayerEntity player = GameManager.Instance.PlayerEntity;
            return (baseSpeed * (player.Skills.GetLiveSkillValue(DFCareer.Skills.Swimming) / 200f)) + (baseSpeed / 4);
        }

        public float GetClimbingSpeed(float baseSpeed)
        {
            // Climbing effect states "target can climb twice as well" - doubling climbing speed
            Entity.PlayerEntity player = GameManager.Instance.PlayerEntity;
            float climbingBoost = player.IsEnhancedClimbing ? 2f : 1f;
            return (baseSpeed / 3) * climbingBoost;
        }
    }
}
