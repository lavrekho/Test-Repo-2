using System;
using System.Collections.Generic;

namespace MailChecker
{
    public static class Sorter
    {
        public static void SortDesc(ref int[] array)
        {
            List<int> sorted = new List<int>(array);

            int greater, lower;

            for (int i = 0, lng = sorted.Count; i < lng; i++)
            {
                greater = sorted[i];

                for (int j = 0; j < i; j++)
                {
                    lower = sorted[j];

                    if (greater > lower)
                    {
                        sorted.Insert(j, greater);
                        sorted.RemoveAt(i + 1);
                        break;
                    }
                }
            }

            array = sorted.ToArray();
        }
    }
}