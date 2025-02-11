﻿using BossMod;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UIDev.Analysis
{
    class TetherInfo
    {
        private class TetherData
        {
            public HashSet<uint> SourceOIDs = new();
            public HashSet<uint> TargetOIDs = new();
        }

        private Type? _oidType;
        private Type? _tidType;
        private Dictionary<uint, TetherData> _data = new();

        public TetherInfo(List<Replay> replays, uint oid)
        {
            var moduleType = ModuleRegistry.TypeForOID(oid);
            _oidType = moduleType?.Module.GetType($"{moduleType.Namespace}.OID");
            _tidType = moduleType?.Module.GetType($"{moduleType.Namespace}.TetherID");
            foreach (var replay in replays)
            {
                foreach (var enc in replay.Encounters.Where(enc => enc.OID == oid))
                {
                    foreach (var tether in replay.EncounterTethers(enc))
                    {
                        var data = _data.GetOrAdd(tether.ID);
                        if (tether.Source != null)
                            data.SourceOIDs.Add(tether.Source.OID);
                        if (tether.Target != null)
                            data.TargetOIDs.Add(tether.Target.OID);
                    }
                }
            }
        }

        public void Draw(Tree tree)
        {
            foreach (var (tid, data) in tree.Nodes(_data, kv => ($"{kv.Key} ({_tidType?.GetEnumName(kv.Key)})", false)))
            {
                tree.LeafNode($"Source IDs: {OIDListString(data.SourceOIDs)}");
                tree.LeafNode($"Target IDs: {OIDListString(data.TargetOIDs)}");
            }
        }

        public void DrawContextMenu()
        {
            if (ImGui.MenuItem("Generate enum for boss module"))
            {
                var sb = new StringBuilder("public enum TetherID : uint\n{");
                foreach (var (tid, data) in _data)
                {
                    var name = _tidType?.GetEnumName(tid) ?? $"_Gen_Tether_{tid}";
                    sb.Append($"\n    {name} = {tid}, // {OIDListString(data.SourceOIDs)}->{OIDListString(data.TargetOIDs)}");
                }
                sb.Append("\n};\n");
                ImGui.SetClipboardText(sb.ToString());
            }
        }

        private string OIDListString(IEnumerable<uint> oids)
        {
            var s = string.Join('/', oids.Select(oid => oid == 0 ? "player" : _oidType?.GetEnumName(oid) ?? $"{oid:X}"));
            return s.Length > 0 ? s : "none";
        }
    }
}
