using System;
using System.Collections.Generic;
using System.Reflection;
namespace HarmonyLib {public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n){}}}
namespace UnityEngine {public class GameObject{public bool TryGetComponent<T>(out T value){value=default;return true;}}}
public class ItemDrop{}
public class CharacterDrop{public class Drop{public UnityEngine.GameObject m_prefab=new();public int m_amountMin=1,m_amountMax=2;public float m_chance=.2f;public bool m_onePerPlayer,m_levelMultiplier=true,m_levelMultiplierNotAmount,m_dontScale;}public void GenerateDropList(){}}
namespace ValheimPlus.Configurations{public class Configuration{public static Configuration Current=new();public Loot LootDrop=new();}public class Loot{public bool IsEnabled=true;public float lootDropAmountMultiplier=100,lootDropChanceMultiplier=100;}}
namespace ValheimPlus{public static class Helper{public static float applyModifierValue(float value,float modifier)=>value*(1+modifier/100);}}
class Program{static void Main(){foreach(bool chanceOnly in new[]{false,true}){var original=new List<CharacterDrop.Drop>{new(){m_levelMultiplierNotAmount=chanceOnly,m_onePerPlayer=true,m_dontScale=true}};var args=new object[]{original,null};var t=typeof(ValheimPlus.CharacterDrop_GenerateDropList_Patch);t.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);var copy=((List<CharacterDrop.Drop>)args[0])[0];if(copy.m_levelMultiplierNotAmount!=chanceOnly||copy.m_amountMin!=2||copy.m_amountMax!=4||copy.m_chance!=.4f||!copy.m_onePerPlayer||!copy.m_dontScale)throw new Exception("Incorrect drop copy");t.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);if(!ReferenceEquals(args[0],original))throw new Exception("Original drops not restored");Console.WriteLine("PASS drop chance/amount level mode preserved: "+chanceOnly);}}}
