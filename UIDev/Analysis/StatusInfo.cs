﻿using BossMod;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UIDev.Analysis
{
    class StatusInfo
    {
        private class StatusData
        {
            public HashSet<uint> SourceOIDs = new();
            public HashSet<uint> TargetOIDs = new();
            public HashSet<ushort> Extras = new();
        }

        private Type? _oidType;
        private Type? _sidType;
        private Dictionary<uint, StatusData> _data = new();

        public StatusInfo(List<Replay> replays, uint oid)
        {
            var moduleType = ModuleRegistry.TypeForOID(oid);
            _oidType = moduleType?.Module.GetType($"{moduleType.Namespace}.OID");
            _sidType = moduleType?.Module.GetType($"{moduleType.Namespace}.SID");
            foreach (var replay in replays)
            {
                foreach (var enc in replay.Encounters.Where(enc => enc.OID == oid))
                {
                    foreach (var status in replay.EncounterStatuses(enc).Where(s => !(s.Source?.Type is ActorType.Player or ActorType.Pet or ActorType.Chocobo) && !(s.Target?.Type is ActorType.Pet or ActorType.Chocobo)))
                    {
                        var data = _data.GetOrAdd(status.ID);
                        if (status.Source != null)
                            data.SourceOIDs.Add(status.Source.OID);
                        if (status.Target != null)
                            data.TargetOIDs.Add(status.Target.OID);
                        data.Extras.Add(status.StartingExtra);
                    }
                }
            }
        }

        public void Draw(Tree tree)
        {
            foreach (var (sid, data) in tree.Nodes(_data, kv => ($"{Utils.StatusString(kv.Key)} ({_sidType?.GetEnumName(kv.Key)})", false)))
            {
                tree.LeafNode($"Source IDs: {OIDListString(data.SourceOIDs)}");
                tree.LeafNode($"Target IDs: {OIDListString(data.TargetOIDs)}");
                tree.LeafNode($"Extras: {string.Join(", ", data.Extras.Select(extra => $"{extra:X}"))}");
            }
        }

        public void DrawContextMenu()
        {
            if (ImGui.MenuItem("Generate enum for boss module"))
            {
                var sb = new StringBuilder("public enum SID : uint\n{");
                foreach (var (sid, data) in _data)
                {
                    string name = _sidType?.GetEnumName(sid) ?? $"_Gen_{Service.LuminaGameData?.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>()?.GetRow(sid)?.Name.ToString() ?? $"Status_{sid}"}";
                    sb.Append($"\n    {name.Replace(' ', '_')} = {sid}, // {OIDListString(data.SourceOIDs)}->{OIDListString(data.TargetOIDs)}");

                    var extras = string.Join('/', data.Extras.Select(extra => $"0x{extra:X}"));
                    if (extras.Length > 0)
                        sb.Append($", extra={extras}");
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
