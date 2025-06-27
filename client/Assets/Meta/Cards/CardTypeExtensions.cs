using System;
using System.Collections.Generic;
using Internal;
using Shared;

namespace Meta
{
    public static class CardTypeExtensions
    {
        static CardTypeExtensions()
        {
            var values = Enum.GetValues(typeof(CardType));
            var list = new List<CardType>();

            foreach (var type in values)
            {
                list.Add((CardType)type);
            }

            list = new List<CardType>()
            {
                CardType.ZipZap,
                CardType.ZipZap_Max
            };

            All = list;
        }

        private static readonly IReadOnlyList<CardType> All;

        public static CardType GetRandom()
        {
            return All.Random();
        }
    }
}