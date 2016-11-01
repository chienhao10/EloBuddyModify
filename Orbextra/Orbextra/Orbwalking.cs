﻿namespace Orbextra
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SharpDX;
    using EloBuddy;
    using EloBuddy.SDK;
    using EloBuddy.SDK.Menu;
    using EloBuddy.SDK.Menu.Values;

    //using Color = System.Drawing.Color;
    using System.Net;
    using EloBuddy.SDK.Rendering;

    /// <summary>
    ///     This class offers everything related to auto-attacks and orbwalking.
    /// </summary>
    public static class Orbwalking
    {
        #region Static Fields

        public static AIHeroClient ForcedTarget = null;

        /// <summary>
        ///     <c>true</c> if the orbwalker will attack.
        /// </summary>
        public static bool Attack = true;

        /// <summary>
        ///     <c>true</c> if the orbwalker will skip the next attack.
        /// </summary>
        public static bool DisableNextAttack;

        /// <summary>
        ///     The last auto attack tick
        /// </summary>
        public static int LastAATick;

        /// <summary>
        ///     The tick the most recent attack command was sent.
        /// </summary>
        public static int LastAttackCommandT;

        /// <summary>
        ///     The last move command position
        /// </summary>
        public static Vector3 LastMoveCommandPosition = Vector3.Zero;

        /// <summary>
        ///     The tick the most recent move command was sent.
        /// </summary>
        public static int LastMoveCommandT;

        /// <summary>
        ///     <c>true</c> if the orbwalker will move.
        /// </summary>
        public static bool Move = true;

        /// <summary>
        ///     The champion name
        /// </summary>
        private static readonly string _championName;

        /// <summary>
        ///     The random
        /// </summary>
        private static readonly Random _random = new Random(DateTime.Now.Millisecond);

        /// <summary>
        ///     Spells that reset the attack timer.
        /// </summary>
        private static readonly string[] AttackResets =
            {
                "dariusnoxiantacticsonh", "fiorae", "garenq", "gravesmove",
                "hecarimrapidslash", "jaxempowertwo", "jaycehypercharge",
                "leonashieldofdaybreak", "luciane", "monkeykingdoubleattack",
                "mordekaisermaceofspades", "nasusq", "nautiluspiercinggaze",
                "netherblade", "gangplankqwrapper", "powerfist",
                "renektonpreexecute", "rengarq", "shyvanadoubleattack",
                "sivirw", "takedown", "talonnoxiandiplomacy",
                "trundletrollsmash", "vaynetumble", "vie", "volibearq",
                "xenzhaocombotarget", "yorickspectral", "reksaiq",
                "itemtitanichydracleave", "masochism", "illaoiw",
                "elisespiderw", "fiorae", "meditate", "sejuaninorthernwinds",
                "asheq"
            };

        /// <summary>
        ///     Spells that are attacks even if they dont have the "attack" word in their name.
        /// </summary>
        private static readonly string[] Attacks =
            {
                "caitlynheadshotmissile", "frostarrow", "garenslash2",
                "kennenmegaproc", "masteryidoublestrike", "quinnwenhanced",
                "renektonexecute", "renektonsuperexecute",
                "rengarnewpassivebuffdash", "trundleq", "xenzhaothrust",
                "xenzhaothrust2", "xenzhaothrust3", "viktorqbuff",
                "lucianpassiveshot"
            };

        /// <summary>
        ///     Spells that are not attacks even if they have the "attack" word in their name.
        /// </summary>
        private static readonly string[] NoAttacks =
            {
                "volleyattack", "volleyattackwithsound",
                "jarvanivcataclysmattack", "monkeykingdoubleattack",
                "shyvanadoubleattack", "shyvanadoubleattackdragon",
                "zyragraspingplantattack", "zyragraspingplantattack2",
                "zyragraspingplantattackfire", "zyragraspingplantattack2fire",
                "viktorpowertransfer", "sivirwattackbounce", "asheqattacknoonhit",
                "elisespiderlingbasicattack", "heimertyellowbasicattack",
                "heimertyellowbasicattack2", "heimertbluebasicattack",
                "annietibbersbasicattack", "annietibbersbasicattack2",
                "yorickdecayedghoulbasicattack", "yorickravenousghoulbasicattack",
                "yorickspectralghoulbasicattack", "malzaharvoidlingbasicattack",
                "malzaharvoidlingbasicattack2", "malzaharvoidlingbasicattack3",
                "kindredwolfbasicattack"
            };

        /// <summary>
        ///     Champs whose auto attacks can't be cancelled
        /// </summary>
        private static readonly string[] NoCancelChamps = { "Kalista" };

        /// <summary>
        ///     The player
        /// </summary>
        private static readonly AIHeroClient Player;

        private static int _autoattackCounter;

        /// <summary>
        ///     The delay
        /// </summary>
        private static int _delay;

        /// <summary>
        ///     The last target
        /// </summary>
        private static AttackableUnit _lastTarget;

        /// <summary>
        ///     The minimum distance
        /// </summary>
        private static float _minDistance = 400;

        /// <summary>
        ///     <c>true</c> if the auto attack missile was launched from the player.
        /// </summary>
        private static bool _missileLaunched;

        #endregion

        #region Constructors and Destructors
        /// <summary>
        ///     Initializes static members of the <see cref="Orbwalking" /> class.
        /// </summary>
        static Orbwalking()
        {

            Player = EloBuddy.Player.Instance;
            _championName = Player.ChampionName;
            Obj_AI_Base.OnProcessSpellCast += new Obj_AI_ProcessSpellCast(OnProcessSpellCast);
            Obj_AI_Base.OnBasicAttack += new Obj_AI_BaseOnBasicAttack(OnBasicAttack);
            Obj_AI_Base.OnSpellCast += new Obj_AI_BaseDoCastSpell(Obj_AI_Base_OnDoCast);
            Spellbook.OnStopCast += new SpellbookStopCast(SpellbookOnStopCast);

            if (Game.Mode == GameMode.Running)
            {
                Game_OnStart(new EventArgs());
            }
            Game.OnLoad += Game_OnStart;

            if (_championName == "Rengar")
            {
                Obj_AI_Base.OnPlayAnimation += delegate (Obj_AI_Base sender, GameObjectPlayAnimationEventArgs args)
                {
                    if (sender.IsMe && args.Animation == "Spell5")
                    {
                        var t = 0;

                        if (_lastTarget != null && _lastTarget.IsValid)
                        {
                            t += (int)Math.Min(EloBuddy.Player.Instance.Distance(_lastTarget) / 1.5f, 0.6f);
                        }

                        LastAATick = Core.GameTickCount - Game.Ping / 2 + t;
                    }
                };
            }
        }

        public static AIHeroClient Player1 { get; private set; }
        public static List<AIHeroClient> AllHeroes { get; private set; }

        static void Game_OnStart(EventArgs args)
        {
            AllHeroes = ObjectManager.Get<AIHeroClient>().ToList();
            Player1 = AllHeroes.Find(x => x.IsMe);
        }

        #endregion

        #region Delegates

        /// <summary>
        ///     Delegate AfterAttackEvenH
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="target">The target.</param>
        public delegate void AfterAttackEvenH(AttackableUnit unit, AttackableUnit target);

        /// <summary>
        ///     Delegate BeforeAttackEvenH
        /// </summary>
        /// <param name="args">The <see cref="BeforeAttackEventArgs" /> instance containing the event data.</param>
        public delegate void BeforeAttackEvenH(BeforeAttackEventArgs args);

        /// <summary>
        ///     Delegate OnAttackEvenH
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="target">The target.</param>
        public delegate void OnAttackEvenH(AttackableUnit unit, AttackableUnit target);

        /// <summary>
        ///     Delegate OnNonKillableMinionH
        /// </summary>
        /// <param name="minion">The minion.</param>
        public delegate void OnNonKillableMinionH(AttackableUnit minion);

        /// <summary>
        ///     Delegate OnTargetChangeH
        /// </summary>
        /// <param name="oldTarget">The old target.</param>
        /// <param name="newTarget">The new target.</param>
        public delegate void OnTargetChangeH(AttackableUnit oldTarget, AttackableUnit newTarget);

        #endregion

        #region Public Events

        /// <summary>
        ///     This event is fired after a unit finishes auto-attacking another unit (Only works with player for now).
        /// </summary>
        public static event AfterAttackEvenH AfterAttack;

        /// <summary>
        ///     This event is fired before the player auto attacks.
        /// </summary>
        public static event BeforeAttackEvenH BeforeAttack;

        /// <summary>
        ///     This event is fired when a unit is about to auto-attack another unit.
        /// </summary>
        public static event OnAttackEvenH OnAttack;

        /// <summary>
        ///     Occurs when a minion is not killable by an auto attack.
        /// </summary>
        public static event OnNonKillableMinionH OnNonKillableMinion;

        /// <summary>
        ///     Gets called on target changes
        /// </summary>
        public static event OnTargetChangeH OnTargetChange;

        #endregion

        #region Enums

        /// <summary>
        ///     The orbwalking mode.
        /// </summary>
        public enum OrbwalkingMode
        {
            /// <summary>
            ///     The orbwalker will only last hit minions.
            /// </summary>
            LastHit,

            /// <summary>
            ///     The orbwalker will alternate between last hitting and auto attacking champions.
            /// </summary>
            Mixed,

            /// <summary>
            ///     The orbwalker will clear the lane of minions as fast as possible while attempting to get the last hit.
            /// </summary>
            LaneClear,

            /// <summary>
            ///     The orbwalker will only attack the target.
            /// </summary>
            Combo,

            /// <summary>
            ///     The orbwalker will only last hit minions as late as possible.
            /// </summary>
            Freeze,

            /// <summary>
            ///     The orbwalker will only move.
            /// </summary>
            CustomMode,

            Flee,

            Burst,

            /// <summary>
            ///     The orbwalker does nothing.
            /// </summary>
            None
        }

        public static AIHeroClient GetBestHeroTarget
        {
            get
            {
                AIHeroClient killableObj = null;
                var hitsToKill = double.MaxValue;
                foreach (var obj in EntityManager.Heroes.Enemies.Where(i => InAutoAttackRange(i)))
                {
                    var killHits = obj.Health / Player.GetAutoAttackDamage(obj, true);
                    if (killableObj != null && (killHits >= hitsToKill || obj.HasBuffOfType(BuffType.Invulnerability)))
                    {
                        continue;
                    }
                    killableObj = obj;
                    hitsToKill = killHits;
                }
                return hitsToKill < 4 ? killableObj : TargetSelector.GetTarget(-1, DamageType.Physical);
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Returns if the player's auto-attack is ready.
        /// </summary>
        /// <returns><c>true</c> if this instance can attack; otherwise, <c>false</c>.</returns>
        public static bool CanAttack()
        {
            /*if (Player.ChampionName == "Graves")
            {
                var attackDelay = 1.0740296828d * 1000 * Player.AttackDelay - 716.2381256175d;
                if (Core.GameTickCount + Game.Ping / 2 + 25 >= LastAATick + attackDelay && Player.HasBuff("GravesBasicAttackAmmo1"))
                {
                    return true;
                }
                return false;
            }//*/

            if (Player.ChampionName == "Jhin")
            {
                if (Player.HasBuff("JhinPassiveReload"))
                {
                    return false;
                }
            }
            /*
            if (Player.IsCastingInterruptableSpell())
            {
                return false;
            }//*/

            return Core.GameTickCount + Game.Ping / 2 + 25 >= LastAATick + Player.AttackDelay * 1000;
        }

        /// <summary>
        ///     Returns true if moving won't cancel the auto-attack.
        /// </summary>
        /// <param name="extraWindup">The extra windup.</param>
        /// <returns><c>true</c> if this instance can move the specified extra windup; otherwise, <c>false</c>.</returns>
        public static bool CanMove(float extraWindup, bool disableMissileCheck = false)
        {
            if (_missileLaunched && Orbwalker.MissileCheck && !disableMissileCheck)
            {
                return true;
            }

            var localExtraWindup = 0;
            if (_championName == "Rengar" && (Player.HasBuff("rengarqbase") || Player.HasBuff("rengarqemp")))
            {
                localExtraWindup = 200;
            }

            return NoCancelChamps.Contains(_championName) || (Core.GameTickCount + Game.Ping / 2 >= LastAATick + Player.AttackCastDelay * 1000 + extraWindup + localExtraWindup);
        }

        /// <summary>
        ///     Returns the auto-attack range of the target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>System.Single.</returns>
        public static float GetAttackRange(AIHeroClient target)
        {
            var result = target.AttackRange + target.BoundingRadius;
            return result;
        }

        /// <summary>
        ///     Gets the last move position.
        /// </summary>
        /// <returns>Vector3.</returns>
        public static Vector3 GetLastMovePosition()
        {
            return LastMoveCommandPosition;
        }

        /// <summary>
        ///     Gets the last move time.
        /// </summary>
        /// <returns>System.Single.</returns>
        public static float GetLastMoveTime()
        {
            return LastMoveCommandT;
        }

        /// <summary>
        ///     Returns player auto-attack missile speed.
        /// </summary>
        /// <returns>System.Single.</returns>
        public static float GetMyProjectileSpeed()
        {
            return IsMelee(Player) || _championName == "Azir" || _championName == "Velkoz"
                   || _championName == "Viktor" && Player.HasBuff("ViktorPowerTransferReturn")
                       ? float.MaxValue
                       : Player.BasicAttack.MissileSpeed;
        }

        /// <summary>
        ///     Returns the auto-attack range of local player with respect to the target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>System.Single.</returns>
        public static float GetRealAutoAttackRange(AttackableUnit target)
        {
            var result = Player.AttackRange + Player.BoundingRadius;
            if (target.IsValidTarget(null, true, null))
            {
                var aiBase = target as Obj_AI_Base;
                if (aiBase != null && Player.ChampionName == "Caitlyn")
                {
                    if (aiBase.HasBuff("caitlynyordletrapinternal"))
                    {
                        result += 650;
                    }
                }

                return result + target.BoundingRadius;
            }

            return result;
        }

        /// <summary>
        ///     Returns true if the target is in auto-attack range.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool InAutoAttackRange(AttackableUnit target)
        {
            if (!target.IsValidTarget(null, true, null))
            {
                return false;
            }
            var myRange = GetRealAutoAttackRange(target);
            return
                Vector2.DistanceSquared(
                    target is Obj_AI_Base ? ((Obj_AI_Base)target).ServerPosition.To2D() : target.Position.To2D(),
                    Player.ServerPosition.To2D()) <= myRange * myRange;
        }

        public static float GetAutoAttackRange(AttackableUnit target = null)
        {
            return GetAutoAttackRange(Player, target);
        }
        public static bool InAutoAttackRange(AttackableUnit target, float extraRange = 0, Vector3 from = new Vector3())
        {
            return target.IsValidTarget(GetAutoAttackRange(target) + extraRange, true, from);
        }
        private static float GetAutoAttackRange(Obj_AI_Base source, AttackableUnit target)
        {
            return source.AttackRange + source.BoundingRadius + (target.IsValidTarget(null, true, null) ? target.BoundingRadius : 0);
        }


        /// <summary>
        ///     Returns true if the spellname is an auto-attack.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns><c>true</c> if the name is an auto attack; otherwise, <c>false</c>.</returns>
        public static bool IsAutoAttack(string name)
        {
            return (name.ToLower().Contains("attack") && !NoAttacks.Contains(name.ToLower())) || Attacks.Contains(name.ToLower());
        }

        /// <summary>
        ///     Returns true if the spellname resets the attack timer.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns><c>true</c> if the specified name is an auto attack reset; otherwise, <c>false</c>.</returns>
        public static bool IsAutoAttackReset(string name)
        {
            return AttackResets.Contains(name.ToLower());
        }

        /// <summary>
        ///     Returns true if the unit is melee
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <returns><c>true</c> if the specified unit is melee; otherwise, <c>false</c>.</returns>
        public static bool IsMelee(this Obj_AI_Base unit)
        {
            return unit.CombatType == GameObjectCombatType.Melee;
        }

        /// <summary>
        ///     Moves to the position.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="holdAreaRadius">The hold area radius.</param>
        /// <param name="overrideTimer">if set to <c>true</c> [override timer].</param>
        /// <param name="useFixedDistance">if set to <c>true</c> [use fixed distance].</param>
        /// <param name="randomizeMinDistance">if set to <c>true</c> [randomize minimum distance].</param>
        public static void MoveTo(
            Vector3 position,
            float holdAreaRadius = 0,
            bool overrideTimer = false,
            bool useFixedDistance = true,
            bool randomizeMinDistance = true)
        {
            var playerPosition = Player.ServerPosition;

            if (playerPosition.Distance(position, true) < holdAreaRadius * holdAreaRadius)
            {
                if (Player.Path.Length > 0)
                {
                    EloBuddy.Player.IssueOrder(GameObjectOrder.Stop, playerPosition);
                    LastMoveCommandPosition = playerPosition;
                    LastMoveCommandT = Core.GameTickCount - 70;
                }
                return;
            }

            var point = position;

            if (Player.Distance(point, true) < 150 * 150)
            {
                point = playerPosition.Extend(position, randomizeMinDistance ? (_random.NextFloat(0.6f, 1) + 0.2f) * _minDistance : _minDistance).To3DWorld();
            }
            var angle = 0f;
            var currentPath = Player.GetWaypoints();
            if (currentPath.Count > 1 && currentPath.PathLength() > 100)
            {
                var movePath = Player.GetPath(point);

                if (movePath.Length > 1)
                {
                    var v1 = currentPath[1] - currentPath[0];
                    var v2 = movePath[1] - movePath[0];
                    angle = v1.AngleBetween(v2.To2D());
                    var distance = movePath.Last().To2D().Distance(currentPath.Last(), true);

                    if ((angle < 10 && distance < 500 * 500) || distance < 50 * 50)
                    {
                        return;
                    }
                }
            }

            if (Core.GameTickCount - LastMoveCommandT < 70 + Math.Min(60, Game.Ping) /*&& !overrideTimer*/ && angle < 60)
            {
                return;
            }

            if (angle >= 60 && Core.GameTickCount - LastMoveCommandT < 60)
            {
                return;
            }

            EloBuddy.Player.IssueOrder(GameObjectOrder.MoveTo, point);
            LastMoveCommandPosition = point;
            LastMoveCommandT = Core.GameTickCount;
        }

        /// <summary>
        ///     Orbwalks a target while moving to Position.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="position">The position.</param>
        /// <param name="extraWindup">The extra windup.</param>
        /// <param name="holdAreaRadius">The hold area radius.</param>
        /// <param name="useFixedDistance">if set to <c>true</c> [use fixed distance].</param>
        /// <param name="randomizeMinDistance">if set to <c>true</c> [randomize minimum distance].</param>
        public static void Orbwalk(
            AttackableUnit target,
            Vector3 position,
            float extraWindup = 90,
            float holdAreaRadius = 0,
            bool useFixedDistance = true,
            bool randomizeMinDistance = true)
        {
            if (Core.GameTickCount - LastAttackCommandT < 70 + Math.Min(60, Game.Ping))
            {
                return;
            }

            try
            {
                if (target.IsValidTarget(null, true, null) && Attack && CanAttack())
                {
                    DisableNextAttack = false;
                    FireBeforeAttack(target);

                    if (!DisableNextAttack)
                    {
                        if (!NoCancelChamps.Contains(_championName))
                        {
                            _missileLaunched = false;
                        }

                        if (EloBuddy.Player.IssueOrder(GameObjectOrder.AttackUnit, target))
                        {
                            LastAttackCommandT = Core.GameTickCount;
                            _lastTarget = target;
                        }

                        return;
                    }
                }

                if (CanMove(extraWindup) && Move)
                {
                    if (Orbwalker.LimitAttackSpeed && (Player.AttackDelay < 1 / 2.6f) && _autoattackCounter % 3 != 0 && !CanMove(500, true))
                    {
                        return;
                    }

                    MoveTo(position, Math.Max(holdAreaRadius, 30), false, useFixedDistance, randomizeMinDistance);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        ///     Resets the Auto-Attack timer.
        /// </summary>
        public static void ResetAutoAttackTimer()
        {
            LastAATick = 0;
        }

        /// <summary>
        ///     Sets the minimum orbwalk distance.
        /// </summary>
        /// <param name="d">The d.</param>
        public static void SetMinimumOrbwalkDistance(float d)
        {
            _minDistance = d;
        }

        /// <summary>
        ///     Sets the movement delay.
        /// </summary>
        /// <param name="delay">The delay.</param>
        public static void SetMovementDelay(int delay)
        {
            _delay = delay;
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Fires the after attack event.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="target">The target.</param>
        private static void FireAfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (AfterAttack != null && target.IsValidTarget(null, true, null))
            {
                AfterAttack(unit, target);
            }
        }

        /// <summary>
        ///     Fires the before attack event.
        /// </summary>
        /// <param name="target">The target.</param>
        private static void FireBeforeAttack(AttackableUnit target)
        {
            if (BeforeAttack != null)
            {
                BeforeAttack(new BeforeAttackEventArgs { Target = target });
            }
            else
            {
                DisableNextAttack = false;
            }
        }

        /// <summary>
        ///     Fires the on attack event.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="target">The target.</param>
        private static void FireOnAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (OnAttack != null)
            {
                OnAttack(unit, target);
            }
        }

        /// <summary>
        ///     Fires the on non killable minion event.
        /// </summary>
        /// <param name="minion">The minion.</param>
        private static void FireOnNonKillableMinion(AttackableUnit minion)
        {
            if (OnNonKillableMinion != null)
            {
                OnNonKillableMinion(minion);
            }
        }

        /// <summary>
        ///     Fires the on target switch event.
        /// </summary>
        /// <param name="newTarget">The new target.</param>
        private static void FireOnTargetSwitch(AttackableUnit newTarget)
        {
            if (OnTargetChange != null && (!_lastTarget.IsValidTarget(null, true, null) || _lastTarget != newTarget))
            {
                OnTargetChange(_lastTarget, newTarget);
            }
        }

        /// <summary>
        ///     Fired when an auto attack is fired.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="GameObjectProcessSpellCastEventArgs" /> instance containing the event data.</param>
        private static void Obj_AI_Base_OnDoCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                var ping = Game.Ping;
                if (ping <= 30) //First world problems kappa
                {
                    Core.DelayAction(() => Obj_AI_Base_OnDoCast_Delayed(sender, args), 30 - ping);
                    return;
                }

                Obj_AI_Base_OnDoCast_Delayed(sender, args);
            }
        }

        /// <summary>
        ///     Fired 30ms after an auto attack is launched.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="GameObjectProcessSpellCastEventArgs" /> instance containing the event data.</param>
        private static void Obj_AI_Base_OnDoCast_Delayed(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {

            if (IsAutoAttackReset(args.SData.Name))
            {
                ResetAutoAttackTimer();
            }

            if (IsAutoAttack(args.SData.Name))
            {
                FireAfterAttack(sender, args.Target as AttackableUnit);
                _missileLaunched = true;
            }
        }

        /// <summary>
        ///     Handles the <see cref="E:ProcessSpell" /> event.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="Spell">The <see cref="GameObjectProcessSpellCastEventArgs" /> instance containing the event data.</param>
        private static void OnProcessSpellCast(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs Spell)
        {
            try
            {
                var spellName = Spell.SData.Name;

                if (unit.IsMe && IsAutoAttackReset(spellName) && Math.Abs(Spell.SData.CastTime) < 1.401298E-45f)
                {
                    ResetAutoAttackTimer();
                }
                if (!IsAutoAttack(spellName))
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        internal static void OnBasicAttack(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && (args.Target is Obj_AI_Base || args.Target is Obj_BarracksDampener || args.Target is Obj_HQ))
            {
                LastAATick = Core.GameTickCount - Game.Ping / 2;
                _missileLaunched = false;
                LastMoveCommandT = 0;
                _autoattackCounter++;

                if (args.Target is Obj_AI_Base)
                {
                    var target = (Obj_AI_Base)args.Target;
                    if (target.IsValid)
                    {
                        FireOnTargetSwitch(target);
                        _lastTarget = target;
                    }
                }

                if (sender is Obj_AI_Turret && args.Target is Obj_AI_Base)
                {
                    LastTargetTurrets[sender.NetworkId] = (Obj_AI_Base)args.Target;
                }
            }
            FireOnAttack(sender, _lastTarget);
        }

        internal static readonly Dictionary<int, Obj_AI_Base> LastTargetTurrets = new Dictionary<int, Obj_AI_Base>();

        /// <summary>
        ///     Fired when the spellbook stops casting a spell.
        /// </summary>
        /// <param name="spellbook">The spellbook.</param>
        /// <param name="args">The <see cref="SpellbookStopCastEventArgs" /> instance containing the event data.</param>
        private static void SpellbookOnStopCast(Obj_AI_Base spellbook, SpellbookStopCastEventArgs args)
        {
            if (spellbook.IsValid && spellbook.IsMe && EloBuddy.SDK.Orbwalker.IsRanged && args.DestroyMissile && args.StopAnimation && !EloBuddy.SDK.Orbwalker.CanBeAborted)// CanCancelAttack)
            {
                ResetAutoAttackTimer();
            }
        }

        #endregion

        /// <summary>
        ///     The before attack event arguments.
        /// </summary>
        public class BeforeAttackEventArgs : EventArgs
        {
            #region Fields

            /// <summary>
            ///     The target
            /// </summary>
            public AttackableUnit Target;

            /// <summary>
            ///     The unit
            /// </summary>
            public Obj_AI_Base Unit = EloBuddy.Player.Instance;

            /// <summary>
            ///     <c>true</c> if the orbwalker should continue with the attack.
            /// </summary>
            private bool _process = true;

            #endregion

            #region Public Properties

            /// <summary>
            ///     Gets or sets a value indicating whether this <see cref="BeforeAttackEventArgs" /> should continue with the attack.
            /// </summary>
            /// <value><c>true</c> if the orbwalker should continue with the attack; otherwise, <c>false</c>.</value>
            public bool Process
            {
                get
                {
                    return this._process;
                }
                set
                {
                    DisableNextAttack = !value;
                    this._process = value;
                }
            }

            #endregion
        }

        /// <summary>
        ///     This class allows you to add an instance of "Orbwalker" to your assembly in order to control the orbwalking in an
        ///     easy way.
        /// </summary>
        public class Orbwalker : IDisposable
        {
            #region Constants

            /// <summary>
            ///     The lane clear wait time modifier.
            /// </summary>
            private const float LaneClearWaitTimeMod = 2f;

            #endregion

            #region Static Fields

            /// <summary>
            ///     The instances of the orbwalker.
            /// </summary>
            public static List<Orbwalker> Instances = new List<Orbwalker>();

            /// <summary>
            ///     The configuration
            /// </summary>
            private static Menu _config, drawings, misc;

            #endregion

            #region Fields

            /// <summary>
            ///     The player
            /// </summary>
            private readonly AIHeroClient Player;

            /// <summary>
            ///     The forced target
            /// </summary>
            private Obj_AI_Base _forcedTarget;

            /// <summary>
            ///     The orbalker mode
            /// </summary>
            private OrbwalkingMode _mode = OrbwalkingMode.None;

            /// <summary>
            ///     The orbwalking point
            /// </summary>
            private Vector3 _orbwalkingPoint;

            /// <summary>
            ///     The previous minion the orbwalker was targeting.
            /// </summary>
            private Obj_AI_Minion _prevMinion;

            #endregion

            #region Constructors and Destructors

            /// <summary>
            ///     Initializes a new instance of the <see cref="Orbwalker" /> class.
            /// </summary>
            /// <param name="attachToMenu">The menu the orbwalker should attach to.</param>
           
            public Orbwalker(Menu attachToMenu)
            {
                _config = attachToMenu;
                /* Drawings submenu */               
                drawings = _config.AddSubMenu("Draw", "Draw");

                drawings.Add("AACircle", new CheckBox("AACircle"));
                drawings.Add("AACircle2", new CheckBox("Enemy AA circle"));
                drawings.Add("HoldZone", new CheckBox("HoldZone"));
                drawings.Add("AALineWidth", new Slider("Line Width", 2, 1, 6));
                drawings.Add("LastHitHelper", new CheckBox("Last Hit Helper"));
                /* Misc options */

                misc = _config.AddSubMenu("Misc", "Misc");
                misc.Add("HoldPosRadius", new Slider("Hold Position Radius", 50, 50, 250));
                misc.Add("PriorizeFarm", new CheckBox("Priorize farm over harass"));
                misc.Add("AttackWards", new CheckBox("Auto attack wards"));
                misc.Add("AttackPetsnTraps", new CheckBox("Auto attack pets & traps"));

                misc.Add("AttackGPBarrel", new ComboBox("Auto attack gangplank barrel", 1, "Combo and Farming", "Farming", "No"));
                misc.Add("Smallminionsprio", new CheckBox("Jungle clear small first"));
                misc.Add("LimitAttackSpeed", new CheckBox("Don't kite if Attack Speed > 2.5"));

                misc.Add("FocusMinionsOverTurrets", new KeyBind("Focus minions over objectives", false, KeyBind.BindTypes.PressToggle, 'M'));
                /* Missile check */
                _config.Add("MissileCheck", new CheckBox("Use Missile Check"));
                /* Delay sliders */
                _config.Add("ExtraWindup", new Slider("Extra windup time", 80, 0, 200));
                _config.Add("FarmDelay", new Slider("Farm delay", 0, 0 , 200));
                /*Load the menu*/
                _config.Add("Flee", new KeyBind("Flee", false, KeyBind.BindTypes.HoldActive, 'C'));
                _config.Add("LastHit", new KeyBind("Last Hit", false, KeyBind.BindTypes.HoldActive, 'A'));
                _config.Add("Farm", new KeyBind("Mixed", false, KeyBind.BindTypes.HoldActive, 'X'));
                _config.Add("LaneClear", new KeyBind("LaneClear", false, KeyBind.BindTypes.HoldActive, 'V'));
                _config.Add("Orbwalk", new KeyBind("Combo", false, KeyBind.BindTypes.HoldActive, 32));
                _config.Add("Burst", new KeyBind("Burst", false, KeyBind.BindTypes.HoldActive, 'T'));
                _config.Add("FastHarass", new KeyBind("Fast Harass", false, KeyBind.BindTypes.HoldActive, 'Y'));
                _config.Add("StillCombo", new KeyBind("Combo without moving", false, KeyBind.BindTypes.HoldActive, 'N'));

                this.Player = EloBuddy.Player.Instance;
                Game.OnUpdate += new GameUpdate(this.GameOnOnGameUpdate);
                Drawing.OnDraw += new DrawingDraw(this.DrawingOnOnDraw);
                Instances.Add(this);
            }

            public static bool getCheckBoxItem(Menu m, string item)
            {
                return m[item].Cast<CheckBox>().CurrentValue;
            }

            public static int getSliderItem(Menu m, string item)
            {
                return m[item].Cast<Slider>().CurrentValue;
            }

            public static bool getKeyBindItem(Menu m, string item)
            {
                return m[item].Cast<KeyBind>().CurrentValue;
            }

            public static int getBoxItem(Menu m, string item)
            {
                return m[item].Cast<ComboBox>().CurrentValue;
            }
            #endregion

            #region Public Properties

            public static bool LimitAttackSpeed
            {
                get
                {
                    return getCheckBoxItem(misc, "LimitAttackSpeed");
                }
            }

            /// <summary>
            ///     Gets a value indicating whether the orbwalker is orbwalking by checking the missiles.
            /// </summary>
            /// <value><c>true</c> if the orbwalker is orbwalking by checking the missiles; otherwise, <c>false</c>.</value>
            public static bool MissileCheck
            {
                get
                {
                    return getCheckBoxItem(_config, "MissileCheck");
                }
            }

            /// <summary>
            ///     Gets or sets the active mode.
            /// </summary>
            /// <value>The active mode.</value>
            public OrbwalkingMode ActiveMode
            {
                get
                {
                    if (_mode != OrbwalkingMode.None)
                    {
                        return _mode;
                    }

                    if (getKeyBindItem(_config, "Orbwalk"))
                    {
                        return OrbwalkingMode.Combo;
                    }

                    if (getKeyBindItem(_config, "StillCombo"))
                    {
                        return OrbwalkingMode.Combo;
                    }

                    if (getKeyBindItem(_config, "LaneClear"))
                    {
                        return OrbwalkingMode.LaneClear;
                    }

                    if (getKeyBindItem(_config, "Farm"))
                    {
                        return OrbwalkingMode.Mixed;
                    }

                    if (getKeyBindItem(_config, "LastHit"))
                    {
                        return OrbwalkingMode.LastHit;
                    }

                    if (getKeyBindItem(_config, "Flee"))
                    {
                        return OrbwalkingMode.Flee;
                    }

                    if (getKeyBindItem(_config, "Burst"))
                    {
                        return OrbwalkingMode.Burst;
                    }

                    return OrbwalkingMode.None;
                }
                set { _mode = value; }
            }

            #endregion

            #region Properties

            /// <summary>
            ///     Gets the farm delay.
            /// </summary>
            /// <value>The farm delay.</value>
            private int FarmDelay
            {
                get
                {
                    return getSliderItem(_config, "FarmDelay");
                }
            }

            #endregion

            #region Public Methods and Operators

            /// <summary>
            ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                //Menu.Remove(_config);
                Game.OnUpdate -= new GameUpdate(this.GameOnOnGameUpdate);
                Drawing.OnDraw -= new DrawingDraw(this.DrawingOnOnDraw);
                Instances.Remove(this);
            }

            /// <summary>
            ///     Forces the orbwalker to attack the set target if valid and in range.
            /// </summary>
            /// <param name="target">The target.</param>
            public void ForceTarget(Obj_AI_Base target)
            {
                this._forcedTarget = target;
            }

            /// <summary>
            ///     Gets the target.
            /// </summary>
            /// <returns>AttackableUnit.</returns>
            public virtual AttackableUnit GetTarget()
            {
                AttackableUnit result = null;
                var mode = this.ActiveMode;

                if ((mode == OrbwalkingMode.Mixed || mode == OrbwalkingMode.LaneClear)
                    && !getCheckBoxItem(misc, "PriorizeFarm"))
                {
                    var target = TargetSelector.GetTarget(-1, DamageType.Physical);
                    if (target != null && this.InAutoAttackRange(target))
                    {
                        return target;
                    }
                }

                //GankPlank barrels
                var attackGankPlankBarrels = getBoxItem(misc, "AttackGPBarrel");
                if (attackGankPlankBarrels != 2
                    && (attackGankPlankBarrels == 0
                        || (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed
                            || mode == OrbwalkingMode.LastHit || mode == OrbwalkingMode.Freeze)))
                {
                    var enemyGangPlank =
                        EntityManager.Heroes.Enemies.FirstOrDefault(
                            e => e.ChampionName.Equals("gangplank", StringComparison.InvariantCultureIgnoreCase));

                    if (enemyGangPlank != null)
                    {
                        var barrels =
                            ObjectManager.Get<Obj_AI_Minion>()
                                .Where(
                                    minion =>
                                    minion.Team == GameObjectTeam.Neutral
                                    && minion.CharData.BaseSkinName == "gangplankbarrel" && minion.IsHPBarRendered
                                    && minion.IsValidTarget(null, true, null) && this.InAutoAttackRange(minion));

                        foreach (var barrel in barrels)
                        {
                            if (barrel.Health <= 1f)
                            {
                                return barrel;
                            }

                            var t = (int)(this.Player.AttackCastDelay * 1000) + Game.Ping / 2
                                    + 1000 * (int)Math.Max(0, this.Player.Distance(barrel) - this.Player.BoundingRadius)
                                    / (int)GetMyProjectileSpeed();

                            var barrelBuff =
                                barrel.Buffs.FirstOrDefault(
                                    b =>
                                    b.Name.Equals("gangplankebarrelactive", StringComparison.InvariantCultureIgnoreCase));

                            if (barrelBuff != null && barrel.Health <= 2f)
                            {
                                var healthDecayRate = enemyGangPlank.Level >= 13
                                                          ? 0.5f
                                                          : (enemyGangPlank.Level >= 7 ? 1f : 2f);
                                var nextHealthDecayTime = Game.Time < barrelBuff.StartTime + healthDecayRate
                                                              ? barrelBuff.StartTime + healthDecayRate
                                                              : barrelBuff.StartTime + healthDecayRate * 2;

                                if (nextHealthDecayTime <= Game.Time + t / 1000f)
                                {
                                    return barrel;
                                }
                            }
                        }

                        if (barrels.Any())
                        {
                            return null;
                        }
                    }
                }

                /*Killable Minion*/
                if (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed || mode == OrbwalkingMode.LastHit
                    || mode == OrbwalkingMode.Freeze)
                {
                    var MinionList =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(minion => minion.IsValidTarget(null, true, null) && this.InAutoAttackRange(minion))
                            .OrderByDescending(minion => minion.CharData.BaseSkinName.Contains("Siege"))
                            .ThenBy(minion => minion.CharData.BaseSkinName.Contains("Super"))
                            .ThenBy(minion => minion.Health)
                            .ThenByDescending(minion => minion.MaxHealth);

                    foreach (var minion in MinionList)
                    {
                        var t = (int)(this.Player.AttackCastDelay * 1000) - 100 + Game.Ping / 2
                                + 1000 * (int)Math.Max(0, this.Player.Distance(minion) - this.Player.BoundingRadius)
                                / (int)GetMyProjectileSpeed();

                        if (mode == OrbwalkingMode.Freeze)
                        {
                            t += 200 + Game.Ping / 2;
                        }

                        var predHealth = HealthPrediction.GetHealthPrediction(minion, t, this.FarmDelay);

                        if (minion.Team != GameObjectTeam.Neutral && this.ShouldAttackMinion(minion))
                        {
                            var damage = this.Player.GetAutoAttackDamage(minion, true);
                            var killable = predHealth <= damage;

                            if (mode == OrbwalkingMode.Freeze)
                            {
                                if (minion.Health < 50 || predHealth <= 50)
                                {
                                    return minion;
                                }
                            }
                            else
                            {
                                if (predHealth <= 0)
                                {
                                    FireOnNonKillableMinion(minion);
                                }

                                if (killable)
                                {
                                    return minion;
                                }
                            }
                        }
                    }
                }

                //Forced target
                if (this._forcedTarget.IsValidTarget(null, true, null) && this.InAutoAttackRange(this._forcedTarget))
                {
                    return this._forcedTarget;
                }

                /* turrets / inhibitors / nexus */
                if (mode == OrbwalkingMode.LaneClear
                    && (!getKeyBindItem(misc, "FocusMinionsOverTurrets")
                        || !EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy,
                            EloBuddy.Player.Instance.Position,
                            GetRealAutoAttackRange(EloBuddy.Player.Instance)).Any()))
                {
                    /* turrets */
                    foreach (var turret in
                        ObjectManager.Get<Obj_AI_Turret>().Where(t => t.IsValidTarget(null, true, null) && this.InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* inhibitor */
                    foreach (var turret in
                        ObjectManager.Get<Obj_BarracksDampener>()
                            .Where(t => t.IsValidTarget(null, true, null) && this.InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* nexus */
                    foreach (var nexus in
                        ObjectManager.Get<Obj_HQ>().Where(t => t.IsValidTarget(null, true, null) && this.InAutoAttackRange(t)))
                    {
                        return nexus;
                    }
                }

                /*Champions*/
                if (mode != OrbwalkingMode.LastHit && mode != OrbwalkingMode.Flee)
                {
                    if (mode != OrbwalkingMode.LaneClear || !this.ShouldWait())
                    {
                        var target = TargetSelector.GetTarget(from h in EntityManager.Heroes.Enemies
                                                 where h.IsValidTarget(null, false, null) && EloBuddy.Player.Instance.IsInAutoAttackRange(h) && (!EloBuddy.SDK.Orbwalker.IsRanged || Prediction.Position.Collision.GetYasuoWallCollision(EloBuddy.Player.Instance.ServerPosition, h.ServerPosition) == Vector3.Zero)
                                                 select h, DamageType.Physical);

                        if (target.IsValidTarget(null, false, null) && this.InAutoAttackRange(target))
                        {
                            return target;
                        }
                    }
                }

                /*Jungle minions*/
                if (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed)
                {
                    var jminions =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(
                                mob =>
                                mob.IsValidTarget(null, true, null) && mob.Team == GameObjectTeam.Neutral && this.InAutoAttackRange(mob)
                                && mob.CharData.BaseSkinName != "gangplankbarrel" && mob.Name != "WardCorpse");

                    result = getCheckBoxItem(misc, "Smallminionsprio")
                                 ? jminions.MinOrDefault(mob => mob.MaxHealth)
                                 : jminions.MaxOrDefault(mob => mob.MaxHealth);

                    if (result != null)
                    {
                        return result;
                    }
                }

                /* UnderTurret Farming */
                if (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed || mode == OrbwalkingMode.LastHit
                    || mode == OrbwalkingMode.Freeze)
                {
                    var ClosestTower =
                        ObjectManager.Get<Obj_AI_Turret>()
                            .MinOrDefault(t => t.IsAlly && !t.IsDead ? this.Player.Distance(t, true) : float.MaxValue);

                    if (ClosestTower != null && this.Player.Distance(ClosestTower, true) < 1500 * 1500)
                    {
                        Obj_AI_Minion farmUnderTurretMinion = null;
                        Obj_AI_Minion noneKillableMinion = null;
                        // return all the minions underturret in auto attack range
                        var minions = EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy, this.Player.Position, this.Player.AttackRange + 200)
                                .Where(
                                    minion =>
                                    this.InAutoAttackRange(minion) && ClosestTower.Distance(minion, true) < 900 * 900)
                                .OrderByDescending(minion => minion.CharData.BaseSkinName.Contains("Siege"))
                                .ThenBy(minion => minion.CharData.BaseSkinName.Contains("Super"))
                                .ThenByDescending(minion => minion.MaxHealth)
                                .ThenByDescending(minion => minion.Health);
                        if (minions.Any())
                        {
                            // get the turret aggro minion
                            var turretMinion =
                                minions.FirstOrDefault(
                                    minion =>
                                    minion is Obj_AI_Minion && HealthPrediction.HasTurretAggro(minion as Obj_AI_Minion));

                            if (turretMinion != null)
                            {
                                var hpLeftBeforeDie = 0;
                                var hpLeft = 0;
                                var turretAttackCount = 0;
                                var turretStarTick = HealthPrediction.TurretAggroStartTick(
                                    turretMinion as Obj_AI_Minion);
                                // from healthprediction (don't blame me :S)
                                var turretLandTick = turretStarTick + (int)(ClosestTower.AttackCastDelay * 1000)
                                                     + 1000
                                                     * Math.Max(
                                                         0,
                                                         (int)
                                                         (turretMinion.Distance(ClosestTower)
                                                          - ClosestTower.BoundingRadius))
                                                     / (int)(ClosestTower.BasicAttack.MissileSpeed + 70);
                                // calculate the HP before try to balance it
                                for (float i = turretLandTick + 50;
                                     i < turretLandTick + 10 * ClosestTower.AttackDelay * 1000 + 50;
                                     i = i + ClosestTower.AttackDelay * 1000)
                                {
                                    var time = (int)i - Core.GameTickCount + Game.Ping / 2;
                                    var predHP =
                                        (int)
                                        HealthPrediction.LaneClearHealthPrediction(turretMinion, time > 0 ? time : 0);
                                    if (predHP > 0)
                                    {
                                        hpLeft = predHP;
                                        turretAttackCount += 1;
                                        continue;
                                    }
                                    hpLeftBeforeDie = hpLeft;
                                    hpLeft = 0;
                                    break;
                                }
                                // calculate the hits is needed and possibilty to balance
                                if (hpLeft == 0 && turretAttackCount != 0 && hpLeftBeforeDie != 0)
                                {
                                    var damage = (int)this.Player.GetAutoAttackDamage(turretMinion, true);
                                    var hits = hpLeftBeforeDie / damage;
                                    var timeBeforeDie = turretLandTick
                                                        + (turretAttackCount + 1)
                                                        * (int)(ClosestTower.AttackDelay * 1000)
                                                        - Core.GameTickCount;
                                    var timeUntilAttackReady = LastAATick + (int)(this.Player.AttackDelay * 1000)
                                                               > Core.GameTickCount + Game.Ping / 2 + 25
                                                                   ? LastAATick + (int)(this.Player.AttackDelay * 1000)
                                                                     - (Core.GameTickCount + Game.Ping / 2 + 25)
                                                                   : 0;
                                    var timeToLandAttack = this.Player.IsMelee
                                                               ? this.Player.AttackCastDelay * 1000
                                                               : this.Player.AttackCastDelay * 1000
                                                                 + 1000
                                                                 * Math.Max(
                                                                     0,
                                                                     turretMinion.Distance(this.Player)
                                                                     - this.Player.BoundingRadius)
                                                                 / this.Player.BasicAttack.MissileSpeed;
                                    if (hits >= 1
                                        && hits * this.Player.AttackDelay * 1000 + timeUntilAttackReady
                                        + timeToLandAttack < timeBeforeDie)
                                    {
                                        farmUnderTurretMinion = turretMinion as Obj_AI_Minion;
                                    }
                                    else if (hits >= 1
                                             && hits * this.Player.AttackDelay * 1000 + timeUntilAttackReady
                                             + timeToLandAttack > timeBeforeDie)
                                    {
                                        noneKillableMinion = turretMinion as Obj_AI_Minion;
                                    }
                                }
                                else if (hpLeft == 0 && turretAttackCount == 0 && hpLeftBeforeDie == 0)
                                {
                                    noneKillableMinion = turretMinion as Obj_AI_Minion;
                                }
                                // should wait before attacking a minion.
                                if (this.ShouldWaitUnderTurret(noneKillableMinion))
                                {
                                    return null;
                                }
                                if (farmUnderTurretMinion != null)
                                {
                                    return farmUnderTurretMinion;
                                }
                                // balance other minions
                                foreach (var minion in
                                    minions.Where(
                                        x =>
                                        x.NetworkId != turretMinion.NetworkId && x is Obj_AI_Minion
                                        && !HealthPrediction.HasMinionAggro(x as Obj_AI_Minion)))
                                {
                                    var playerDamage = (int)this.Player.GetAutoAttackDamage(minion);
                                    var turretDamage = (int)ClosestTower.GetAutoAttackDamage(minion, true);
                                    var leftHP = (int)minion.Health % turretDamage;
                                    if (leftHP > playerDamage)
                                    {
                                        return minion;
                                    }
                                }
                                // late game
                                var lastminion =
                                    minions.LastOrDefault(
                                        x =>
                                        x.NetworkId != turretMinion.NetworkId && x is Obj_AI_Minion
                                        && !HealthPrediction.HasMinionAggro(x as Obj_AI_Minion));
                                if (lastminion != null && minions.Count() >= 2)
                                {
                                    if (1f / this.Player.AttackDelay >= 1f
                                        && (int)(turretAttackCount * ClosestTower.AttackDelay / this.Player.AttackDelay)
                                        * this.Player.GetAutoAttackDamage(lastminion) > lastminion.Health)
                                    {
                                        return lastminion;
                                    }
                                    if (minions.Count() >= 5 && 1f / this.Player.AttackDelay >= 1.2)
                                    {
                                        return lastminion;
                                    }
                                }
                            }
                            else
                            {
                                if (this.ShouldWaitUnderTurret(noneKillableMinion))
                                {
                                    return null;
                                }
                                // balance other minions
                                foreach (var minion in
                                    minions.Where(
                                        x => x is Obj_AI_Minion && !HealthPrediction.HasMinionAggro(x as Obj_AI_Minion))
                                    )
                                {
                                    if (ClosestTower != null)
                                    {
                                        var playerDamage = (int)this.Player.GetAutoAttackDamage(minion);
                                        var turretDamage = (int)ClosestTower.GetAutoAttackDamage(minion, true);
                                        var leftHP = (int)minion.Health % turretDamage;
                                        if (leftHP > playerDamage)
                                        {
                                            return minion;
                                        }
                                    }
                                }
                                //late game
                                var lastminion =
                                    minions.LastOrDefault(
                                        x => x is Obj_AI_Minion && !HealthPrediction.HasMinionAggro(x as Obj_AI_Minion));
                                if (lastminion != null && minions.Count() >= 2)
                                {
                                    if (minions.Count() >= 5 && 1f / this.Player.AttackDelay >= 1.2)
                                    {
                                        return lastminion;
                                    }
                                }
                            }
                            return null;
                        }
                    }
                }

                /*Lane Clear minions*/
                if (mode == OrbwalkingMode.LaneClear)
                {
                    if (!this.ShouldWait())
                    {
                        if (this._prevMinion.IsValidTarget(null, true, null) && this.InAutoAttackRange(this._prevMinion))
                        {
                            var predHealth = HealthPrediction.LaneClearHealthPrediction(
                                this._prevMinion,
                                (int)(this.Player.AttackDelay * 1000 * LaneClearWaitTimeMod),
                                this.FarmDelay);
                            if (predHealth >= 2 * this.Player.GetAutoAttackDamage(this._prevMinion)
                                || Math.Abs(predHealth - this._prevMinion.Health) < float.Epsilon)
                            {
                                return this._prevMinion;
                            }
                        }

                        result = (from minion in
                                      ObjectManager.Get<Obj_AI_Minion>()
                                      .Where(
                                          minion =>
                                          minion.IsValidTarget(null, true, null) && this.InAutoAttackRange(minion)
                                          && this.ShouldAttackMinion(minion))
                                  let predHealth =
                                      HealthPrediction.LaneClearHealthPrediction(
                                          minion,
                                          (int)(this.Player.AttackDelay * 1000 * LaneClearWaitTimeMod),
                                          this.FarmDelay)
                                  where
                                      predHealth >= 2 * this.Player.GetAutoAttackDamage(minion)
                                      || Math.Abs(predHealth - minion.Health) < float.Epsilon
                                  select minion).MaxOrDefault(
                                      m => !EloBuddy.SDK.Extensions.IsMinion(m)/*, true)*/ ? float.MaxValue : m.Health);

                        if (result != null)
                        {
                            this._prevMinion = (Obj_AI_Minion)result;
                        }
                    }
                }

                return result;
            }


            /// <summary>
            ///     Determines if a target is in auto attack range.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <returns><c>true</c> if a target is in auto attack range, <c>false</c> otherwise.</returns>
            public virtual bool InAutoAttackRange(AttackableUnit target)
            {
                return Orbwalking.InAutoAttackRange(target);
            }

            /// <summary>
            ///     Enables or disables the auto-attacks.
            /// </summary>
            /// <param name="b">if set to <c>true</c> the orbwalker will attack units.</param>
            public void SetAttack(bool b)
            {
                Attack = b;
            }

            /// <summary>
            ///     Enables or disables the movement.
            /// </summary>
            /// <param name="b">if set to <c>true</c> the orbwalker will move.</param>
            public void SetMovement(bool b)
            {
                Move = b;
            }

            /// <summary>
            ///     Forces the orbwalker to move to that point while orbwalking (Game.CursorPos by default).
            /// </summary>
            /// <param name="point">The point.</param>
            public void SetOrbwalkingPoint(Vector3 point)
            {
                this._orbwalkingPoint = point;
            }

            /// <summary>
            ///     Determines if the orbwalker should wait before attacking a minion.
            /// </summary>
            /// <returns><c>true</c> if the orbwalker should wait before attacking a minion, <c>false</c> otherwise.</returns>
            public bool ShouldWait()
            {
                return
                    ObjectManager.Get<Obj_AI_Minion>()
                        .Any(
                            minion =>
                            minion.IsValidTarget(null, true, null) && minion.Team != GameObjectTeam.Neutral
                            && this.InAutoAttackRange(minion) && EloBuddy.SDK.Extensions.IsMinion(minion)//, false)
                            && HealthPrediction.LaneClearHealthPrediction(
                                minion,
                                (int)(this.Player.AttackDelay * 1000 * LaneClearWaitTimeMod),
                                this.FarmDelay) <= this.Player.GetAutoAttackDamage(minion));
            }

            #endregion

            #region Methods

            /// <summary>
            ///     Fired when the game is drawn.
            /// </summary>
            /// <param name="args">The <see cref="EventArgs" /> instance containing the event data.</param>
            private void DrawingOnOnDraw(EventArgs args)
            {
                if (getCheckBoxItem(drawings, "AACircle"))
                {
                    Circle.Draw(Color.LightGreen, GetRealAutoAttackRange(null) + 65, this.Player.Position);
                }
                if (getCheckBoxItem(drawings, "AACircle2"))
                {
                    foreach (var target in EntityManager.Heroes.Enemies.FindAll(target => target.IsValidTarget(1175)))
                    {
                        Circle.Draw(Color.LightGreen, GetAttackRange(target), target.Position);                       
                    }
                }

                if (getCheckBoxItem(drawings, "HoldZone"))
                {
                    Circle.Draw(Color.LightGreen, getSliderItem(misc, "HoldPosRadius"), this.Player.Position);
                }
                
                if (getCheckBoxItem(drawings, "LastHitHelper"))
                {
                    foreach (var minion in
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(
                                x => x.Name.ToLower().Contains("minion") && x.IsHPBarRendered && x.IsValidTarget(1000)))
                    {
                        if (minion.Health < EloBuddy.Player.Instance.GetAutoAttackDamage(minion, true))
                        {
                            Circle.Draw(Color.LimeGreen, 50, minion.Position);
                        }
                    }
                }
            }

            /// <summary>
            ///     Fired when the game is updated.
            /// </summary>
            /// <param name="args">The <see cref="EventArgs" /> instance containing the event data.</param>
            private void GameOnOnGameUpdate(EventArgs args)
            {
                try
                {
                    if (this.ActiveMode == OrbwalkingMode.None)
                    {
                        return;
                    }
                    /*
                    //Prevent canceling important spells
                    if (this.Player.IsCastingInterruptableSpell(true))
                    {
                        return;
                    }//*/

                    var target = this.GetTarget();
                    Orbwalk(target, this._orbwalkingPoint.To2D().IsValid() ? this._orbwalkingPoint : Game.CursorPos,
                        getSliderItem(_config, "ExtraWindup"),
                        Math.Max(getSliderItem(misc, "HoldPosRadius"), 30));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            /// <summary>
            ///     Returns if a minion should be attacked
            /// </summary>
            /// <param name="minion">The <see cref="Obj_AI_Minion" /></param>
            /// <param name="includeBarrel">Include Gangplank Barrel</param>
            /// <returns><c>true</c> if the minion should be attacked; otherwise, <c>false</c>.</returns>
            private bool ShouldAttackMinion(Obj_AI_Minion minion)
            {
                if (minion.Name == "WardCorpse" || minion.CharData.BaseSkinName == "jarvanivstandard")
                {
                    return false;
                }

                if (Extensions.IsWard(minion))
                {
                    return getCheckBoxItem(misc, "AttackWards");
                }

                return (getCheckBoxItem(misc, "AttackPetsnTraps") || Extensions.IsMinion(minion))
                       && minion.CharData.BaseSkinName != "gangplankbarrel";
            }

            private bool ShouldWaitUnderTurret(Obj_AI_Minion noneKillableMinion)
            {
                return
                    ObjectManager.Get<Obj_AI_Minion>()
                        .Any(
                            minion =>
                            (noneKillableMinion != null ? noneKillableMinion.NetworkId != minion.NetworkId : true)
                            && minion.IsValidTarget(null, true, null) && minion.Team != GameObjectTeam.Neutral
                            && this.InAutoAttackRange(minion) && EloBuddy.SDK.Extensions.IsMinion(minion)//, false)
                            && HealthPrediction.LaneClearHealthPrediction(
                                minion,
                                (int)
                                (this.Player.AttackDelay * 1000
                                 + (this.Player.IsMelee
                                        ? this.Player.AttackCastDelay * 1000
                                        : this.Player.AttackCastDelay * 1000
                                          + 1000 * (this.Player.AttackRange + 2 * this.Player.BoundingRadius)
                                          / this.Player.BasicAttack.MissileSpeed)),
                                this.FarmDelay) <= this.Player.GetAutoAttackDamage(minion));
            }

            #endregion
        }
    }
}