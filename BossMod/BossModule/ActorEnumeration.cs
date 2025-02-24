﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BossMod
{
    // for raid members, we support both indexed and non-indexed enumeration
    public static class ActorEnumeration
    {
        // build a mask with set bits corresponding to slots in range
        public static ulong Mask(this IEnumerable<(int, Actor)> range)
        {
            ulong mask = 0;
            foreach ((var i, _) in range)
                BitVector.SetVector64Bit(ref mask, i);
            return mask;
        }

        // filter range with slot+actor by slot or by actor
        public static IEnumerable<(int, Actor)> WhereSlot(this IEnumerable<(int, Actor)> range, Func<int, bool> predicate)
        {
            return range.Where(indexActor => predicate(indexActor.Item1));
        }

        public static IEnumerable<(int, Actor)> WhereActor(this IEnumerable<(int, Actor)> range, Func<Actor, bool> predicate)
        {
            return range.Where(indexActor => predicate(indexActor.Item2));
        }

        // exclude specified actor from enumeration
        public static IEnumerable<Actor> Exclude(this IEnumerable<Actor> range, Actor? actor)
        {
            return range.Where(x => x != actor);
        }

        public static IEnumerable<(int, Actor)> Exclude(this IEnumerable<(int, Actor)> range, Actor? actor)
        {
            return range.WhereActor(x => x != actor);
        }

        public static IEnumerable<(int, Actor)> Exclude(this IEnumerable<(int, Actor)> range, int slot)
        {
            return range.WhereSlot(i => i != slot);
        }

        // select actors that have their corresponding bit in mask set
        public static IEnumerable<(int, Actor)> IncludedInMask(this IEnumerable<(int, Actor)> range, ulong mask)
        {
            return range.WhereSlot(i => BitVector.IsVector64BitSet(mask, i));
        }

        // select actors that have their corresponding bit in mask cleared
        public static IEnumerable<(int, Actor)> ExcludedFromMask(this IEnumerable<(int, Actor)> range, ulong mask)
        {
            return range.WhereSlot(i => !BitVector.IsVector64BitSet(mask, i));
        }

        // select actors in specified radius from specified point
        public static IEnumerable<Actor> InRadius(this IEnumerable<Actor> range, Vector3 origin, float radius)
        {
            return range.Where(actor => GeometryUtils.PointInCircle(actor.Position - origin, radius));
        }

        public static IEnumerable<(int, Actor)> InRadius(this IEnumerable<(int, Actor)> range, Vector3 origin, float radius)
        {
            return range.WhereActor(actor => GeometryUtils.PointInCircle(actor.Position - origin, radius));
        }

        // select actors outside specified radius from specified point
        public static IEnumerable<Actor> OutOfRadius(this IEnumerable<Actor> range, Vector3 origin, float radius)
        {
            return range.Where(actor => !GeometryUtils.PointInCircle(actor.Position - origin, radius));
        }

        public static IEnumerable<(int, Actor)> OutOfRadius(this IEnumerable<(int, Actor)> range, Vector3 origin, float radius)
        {
            return range.WhereActor(actor => !GeometryUtils.PointInCircle(actor.Position - origin, radius));
        }

        // select actors in specified radius from specified actor, excluding actor itself
        public static IEnumerable<Actor> InRadiusExcluding(this IEnumerable<Actor> range, Actor origin, float radius)
        {
            return range.Exclude(origin).InRadius(origin.Position, radius);
        }

        public static IEnumerable<(int, Actor)> InRadiusExcluding(this IEnumerable<(int, Actor)> range, Actor origin, float radius)
        {
            return range.Exclude(origin).InRadius(origin.Position, radius);
        }

        // select actors in specified shape
        public static IEnumerable<Actor> InShape(this IEnumerable<Actor> range, AOEShape shape, Actor origin)
        {
            return range.Where(actor => shape.Check(actor.Position, origin));
        }

        public static IEnumerable<(int, Actor)> InShape(this IEnumerable<(int, Actor)> range, AOEShape shape, Actor origin)
        {
            return range.WhereActor(actor => shape.Check(actor.Position, origin));
        }

        public static IEnumerable<Actor> InShape(this IEnumerable<Actor> range, AOEShape shape, Vector3 origin, float rotation)
        {
            return range.Where(actor => shape.Check(actor.Position, origin, rotation));
        }

        public static IEnumerable<(int, Actor)> InShape(this IEnumerable<(int, Actor)> range, AOEShape shape, Vector3 origin, float rotation)
        {
            return range.WhereActor(actor => shape.Check(actor.Position, origin, rotation));
        }

        // select actors that have tether with specific ID
        public static IEnumerable<Actor> Tethered<ID>(this IEnumerable<Actor> range, ID id) where ID : Enum
        {
            var castID = (uint)(object)id;
            return range.Where(actor => actor.Tether.ID == castID);
        }

        public static IEnumerable<(int, Actor)> Tethered<ID>(this IEnumerable<(int, Actor)> range, ID id) where ID : Enum
        {
            var castID = (uint)(object)id;
            return range.WhereActor(actor => actor.Tether.ID == castID);
        }

        // sort range by distance from point
        public static IEnumerable<Actor> SortedByRange(this IEnumerable<Actor> range, Vector3 origin)
        {
            return range
                .Select(actor => (actor, (actor.Position - origin).LengthSquared()))
                .OrderBy(actorDist => actorDist.Item2)
                .Select(actorDist => actorDist.Item1);
        }

        public static IEnumerable<(int, Actor)> SortedByRange(this IEnumerable<(int, Actor)> range, Vector3 origin)
        {
            return range
                .Select(indexPlayer => (indexPlayer.Item1, indexPlayer.Item2, (indexPlayer.Item2.Position - origin).LengthSquared()))
                .OrderBy(indexPlayerDist => indexPlayerDist.Item3)
                .Select(indexPlayerDist => (indexPlayerDist.Item1, indexPlayerDist.Item2));
        }
    }
}
