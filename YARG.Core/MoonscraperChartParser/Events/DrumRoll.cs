﻿// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    public class DrumRoll : ChartObject
    {
        public enum Type
        {
            Standard,
            Special,
        }

        private readonly ID _classID = ID.DrumRoll;
        public override int classID => (int)_classID;

        public uint length;
        public Type type = Type.Standard;

        public DrumRoll(uint _position, uint _length, Type _type = Type.Standard) : base(_position)
        {
            length = _length;
            type = _type;
        }

        public uint GetCappedLengthForPos(uint pos)
        {
            uint newLength;
            if (pos > tick)
                newLength = pos - tick;
            else
                newLength = 0;

            DrumRoll nextRoll = null;
            if (moonSong != null && moonChart != null)
            {
                int arrayPos = SongObjectHelper.FindClosestPosition(this, moonChart.drumRoll);
                if (arrayPos == SongObjectHelper.NOTFOUND)
                    return newLength;

                while (arrayPos < moonChart.drumRoll.Count - 1 && moonChart.drumRoll[arrayPos].tick <= tick)
                {
                    ++arrayPos;
                }

                if (moonChart.drumRoll[arrayPos].tick > tick)
                    nextRoll = moonChart.drumRoll[arrayPos];

                if (nextRoll != null)
                {
                    // Cap sustain length
                    if (nextRoll.tick < tick)
                        newLength = 0;
                    else if (pos > nextRoll.tick)
                        // Cap sustain
                        newLength = nextRoll.tick - tick;
                }
                // else it's the only drum roll or it's the last drum roll 
            }

            return newLength;
        }
    }
}