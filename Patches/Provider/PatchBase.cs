﻿using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace AV.Inspector
{
	internal abstract class PatchBase
	{
		protected static Assembly EditorAssembly { get; } = typeof(Editor).Assembly;
		
		public enum Apply
		{
			OnLoad,
			OnGUI
		}
		protected class Patch
		{
			public MethodBase original;
			public string prefix;
			public string postfix;
			public string transpiler;
			public string finalizer;
			public Apply apply;
			
			internal bool applied;
			internal MethodInfo harmonyPatch;

			public Patch(MethodBase original, 
				string prefix = null, string postfix = null,
				string transpiler = null, string finalizer = null, 
				Apply apply = Apply.OnLoad)
			{
				this.original = original;
				this.prefix = prefix;
				this.postfix = postfix;
				this.transpiler = transpiler;
				this.finalizer = finalizer;
				this.apply = apply;
			}
		}
		
		private List<Patch> patches;

		// ReSharper disable once EmptyConstructor
		// ReSharper disable once PublicConstructorInAbstractClass
		// default constructor required for activator
		public PatchBase() {}
		
		protected abstract IEnumerable<Patch> GetPatches();

		public void ApplyPatches(Harmony harmony, Apply applyType)
		{
			if (patches == null)
				patches = new List<Patch>();
			
			var type = GetType(); 
			foreach (var patch in GetPatches())
			{
				if (patch.applied)
					continue;
				if (patch.apply != applyType)
					continue;
				
				var original = patch.original;

				if (original == null)
				{
					Debug.LogError($"Null MethodBase for {harmony.Id} patching!");
					continue;
				}
				
				var prefix = AccessTools.Method(type, patch.prefix);
				var postfix = AccessTools.Method(type, patch.postfix);
				var transpiler = AccessTools.Method(type, patch.transpiler);
				var finalizer = AccessTools.Method(type, patch.finalizer);

				var harmonyPatch = harmony.Patch(original,
					prefix != null ? new HarmonyMethod(prefix) : null,
					postfix != null ? new HarmonyMethod(postfix) : null,
					transpiler != null ? new HarmonyMethod(transpiler) : null,
					finalizer != null ? new HarmonyMethod(finalizer) : null
				);
				patch.harmonyPatch = harmonyPatch;
				patch.applied = true;
				
				patches.Add(patch); 
			}
		}

		public void UnpatchAll(Harmony harmony)
		{
			foreach (var patch in patches)
			{
				if (!patch.applied)
					continue;
				patch.applied = false;
				
				var original = patch.original;
				var info = Harmony.GetPatchInfo(original);
				
				info.Prefixes.Do(patchInfo => harmony.Unpatch(original, patchInfo.PatchMethod));
				info.Postfixes.Do(patchInfo => harmony.Unpatch(original, patchInfo.PatchMethod));
				info.Transpilers.Do(patchInfo => harmony.Unpatch(original, patchInfo.PatchMethod));
				info.Finalizers.Do(patchInfo => harmony.Unpatch(original, patchInfo.PatchMethod));
			}
			patches.Clear();
		}
	}
}