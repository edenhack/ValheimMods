using BepInEx;
using BepInEx.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using Common;
using HarmonyLib;
using System;
using fastJSON;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Reflection;
using EpicLoot;
using EpicLoot.Abilities;
using EpicLoot.Adventure;
using EpicLoot.Crafting;
using EpicLoot.GatedItemType;
using EpicLoot.LegendarySystem;
using EpicLoot.MagicItemEffects;
using EpicLoot.MagicItemComponent;
using ExtendedItemDataFramework;
using ServerSync;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using Debug = UnityEngine.Debug;

namespace MultiUtil
{
    [BepInPlugin("knightcuddles.MultiUtil", "Equip multiple EpicLoot Magic Utility Items", "0.0.1")]
    public class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;
        private Harmony harmony;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> maxEquippedItems;


        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug.Value)
                Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        private void Awake()
        {
            context = this;
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            isDebug = Config.Bind<bool>("General", "IsDebug", true, "Enable debug logs");

            maxEquippedItems = Config.Bind<int>("Variables", "MaxEquippedItems", 5, "Maximum number of utility items equipped at once.");

            if (!modEnabled.Value)
                return;

            harmony = new Harmony(Info.Metadata.GUID);
            harmony.PatchAll();
        }


        [HarmonyPatch(typeof(Player), "UpdateElements")]
        static class UpdateElements_Patch
        {
            static void Postfix(Player __instance, ref float limit, ItemDrop.ItemData ___m_utilityItem)
            {
                if (!modEnabled.Value) 
                    return;

                var list = __instance.GetInventory().GetAllItems().FindAll(i => i.m_equiped && i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility && i != ___m_utilityItem);

                foreach (var item in list)
                {
                    return EpicLoot.PlayerExtensions.GetAllActiveMagicEffects;
                }
            }
        }                              

        [HarmonyPatch(typeof(Player), "QueueEquipItem")]
        static class QueueEquipItem_Patch
        {
            static bool Prefix(Player __instance, ItemDrop.ItemData item)
            {
                if (!modEnabled.Value || item == null || __instance.IsItemQueued(item) || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Utility) 
                    return true;

                var items = __instance.GetInventory().GetAllItems().FindAll(i => i.m_equiped && i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility);
                if (items.Exists(i => i.m_shared.m_name == item.m_shared.m_name))
                    return false;

                if (items.Count >= maxEquippedItems.Value)
                    return false;

                return true;
            }
        }                
        
        [HarmonyPatch(typeof(Humanoid), "EquipItem")]
        static class EquipItem_Patch
        {
            static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, bool triggerEquipEffects, Inventory ___m_inventory, ref bool __result, ref ItemDrop.ItemData ___m_utilityItem)
            {
                //Dbgl($"trying to equip item {item.m_shared.m_name}");
                if (!modEnabled.Value || item == null || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Utility ||  !__instance.IsPlayer() || !___m_inventory.ContainsItem(item) || __instance.InAttack() || __instance.InDodge() || (__instance.IsPlayer() && !__instance.IsDead() && __instance.IsSwiming() && !__instance.IsOnGround()) || (item.m_shared.m_useDurability && item.m_durability <= 0f) || (item.m_shared.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(item.m_shared.m_dlc)))
                    return true;

                //Dbgl($"can equip {item.m_shared.m_name}");

                int count = __instance.GetInventory().GetAllItems().FindAll(i => i.m_equiped && i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility).Count;
                if (count >= maxEquippedItems.Value)
                {
                    __result = false;
                    return false;
                }
                if(___m_utilityItem == null)
                {
                    //Dbgl($"setting as utility item {item.m_shared.m_name}");

                    ___m_utilityItem = item;
                }
                item.m_equiped = true;
                typeof(Humanoid).GetMethod("SetupEquipment", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { });
                if (triggerEquipEffects)
                {
                    typeof(Humanoid).GetMethod("TriggerEquipEffect", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { item });
                }
                __result = true;
                //Dbgl($"Equipped {item.m_shared.m_name}");
                return false;
            }
        }                
        

        [HarmonyPatch(typeof(Humanoid), "UpdateEquipmentStatusEffects")]
        static class UpdateEquipmentStatusEffects_Patch
        {
            static void Postfix(Humanoid __instance, ItemDrop.ItemData ___m_utilityItem, ref HashSet<StatusEffect> ___m_eqipmentStatusEffects, SEMan ___m_seman)
            {
                if (!modEnabled.Value || !__instance.IsPlayer())
                    return;

                var list = __instance.GetInventory().GetAllItems().FindAll(i => i.m_equiped && i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility && i != ___m_utilityItem);

                foreach(var item in list)
                {
                    if (!item.m_shared.m_equipStatusEffect)
                        continue;
                    ___m_seman.AddStatusEffect(item.m_shared.m_equipStatusEffect, false);
                }
                //Dbgl($"added {list.Count} effects");
            }
        }                
        
        [HarmonyPatch(typeof(Humanoid), "UnequipAllItems")]
        static class UnequipAllItems_Patch
        {
            static void Postfix(Humanoid __instance)
            {
                if (!modEnabled.Value || !__instance.IsPlayer())
                    return;

                var list = __instance.GetInventory().GetAllItems().FindAll(i => i.m_equiped && i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility);
                foreach (ItemDrop.ItemData item in list)
                    __instance.UnequipItem(item, false);
            }
        }
                    
        [HarmonyPatch(typeof(Humanoid), "IsItemEquiped")]
        static class IsItemEquiped_Patch
        {
            static void Postfix(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!modEnabled.Value || !__instance.IsPlayer() || __result || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Utility)
                    return;
                __result = item.m_equiped;
            }
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip", new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool) })]
        static class GetTooltip_Patch
        {
            static void Postfix(ref ItemDrop.ItemData item, int qualityLevel, ref string __result)
            {
                if (!modEnabled.Value || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Utility)
                    return;

                __result += string.Format("\n\n$item_armor: <color=orange>{0}</color>", item.GetArmor(qualityLevel));
                if(item.m_shared.m_damageModifiers.Count > 0)
                    __result += SE_Stats.GetDamageModifiersTooltipString(item.m_shared.m_damageModifiers);
            }
        }


        [HarmonyPatch(typeof(Terminal), "InputText")]
        static class InputText_Patch
        {
            static bool Prefix(Terminal __instance)
            {
                if (!modEnabled.Value)
                    return true;
                string text = __instance.m_input.text;
                if (text.ToLower().Equals($"{typeof(BepInExPlugin).Namespace.ToLower()} reset"))
                {
                    context.Config.Reload();
                    context.Config.Save();

                    __instance.AddString(text);
                    __instance.AddString($"{context.Info.Metadata.Name} config reloaded");
                    return false;
                }
                return true;
            }
        }
    }
}
