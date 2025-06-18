namespace BestiaryAPI.helpers
{
    public static class helpers {
        public static double FractionToDouble(string fraction)
        {
            double result;

            if (double.TryParse(fraction, out result))
            {
                return result;
            }

            string[] split = fraction.Split(new char[] { ' ', '/' });

            if (split.Length == 2 || split.Length == 3)
            {
                int a, b;

                if (int.TryParse(split[0], out a) && int.TryParse(split[1], out b))
                {
                    if (split.Length == 2)
                    {
                        return (double)a / b;
                    }

                    int c;

                    if (int.TryParse(split[2], out c))
                    {
                        return a + (double)b / c;
                    }
                }
            }

            throw new FormatException("Not a valid fraction.");
        }


        //FOrmat: {<Max level that has this proficiency bonus>, <Proficiency Bonus>}
        // Eg {4,2) means from level 0 to 4, the proficiency bonus is 2
        //The dictionary is meant to be traversed in order - a level of between 8 to 12 would have a proficiency bonus of 4
        public static readonly Dictionary<double, int> cr_prof_dict = new Dictionary<double, int> {
            {4, 2 },
            {8, 3 },
            {12, 4 },
            {16, 5 },
            {20, 6 },
            {24, 7 },
            {25, 8 }


        };

        public static int GetProficiencyBonus(double cr)
        {
            //find the highest key that cr is less than or equal to
            var keys = cr_prof_dict.Keys.OrderBy(k => k).ToList();
            foreach (var key in keys)
            {
                if (cr <= key)
                {
                    return cr_prof_dict[key];
                }
            }
            return 0; //default case, should not happen if data is correct
        }
        
        //returns 5etools bestiary link for given monster name and its source
        public static string build_link(string name, string source)
        {
            string base_url = "https://5e.tools/bestiary.html#";
            base_url += name;
            base_url += "_" + source;
            return base_url;
        }
    }
}
