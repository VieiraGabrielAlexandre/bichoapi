public static class BetHelpers {
    public static string Extract(string milhar, PositionPick pos, string what)
    {
        return (pos, what) switch {
            (PositionPick.RIGHT, "UNIT")   => milhar.Substring(3,1),
            (PositionPick.RIGHT, "DOZEN")  => milhar.Substring(2,2),
            (PositionPick.RIGHT, "HUND")   => milhar.Substring(1,3),
            (PositionPick.RIGHT, "THOU")   => milhar,
            (PositionPick.LEFT,  "DOZEN")  => milhar.Substring(0,2),
            (PositionPick.LEFT,  "HUND")   => milhar.Substring(0,3),
            (PositionPick.MIDDLE,"DOZEN")  => milhar.Substring(1,2),
            _ => throw new ArgumentException("combinação posição/parte inválida")
        };
    }

    public static IEnumerable<int> PrizeIndexes(PrizeWindow w) => w switch {
        PrizeWindow._1_ONLY => new[] {0},
        PrizeWindow._1_3    => new[] {0,1,2},
        PrizeWindow._1_5    => new[] {0,1,2,3,4},
        PrizeWindow._1_6    => new[] {0,1,2,3,4,5},
        _ => Enumerable.Empty<int>()
    };

    public static HashSet<string> Permutations(string s)
    {
        var res = new HashSet<string>();
        void Back(char[] arr, int l) {
            if (l == arr.Length) { res.Add(new string(arr)); return; }
            var used = new HashSet<char>();
            for (int i=l;i<arr.Length;i++){
                if (used.Contains(arr[i])) continue;
                used.Add(arr[i]);
                (arr[l],arr[i]) = (arr[i],arr[l]);
                Back(arr, l+1);
                (arr[l],arr[i]) = (arr[i],arr[l]);
            }
        }
        Back(s.ToCharArray(), 0);
        return res;
    }

    public static HashSet<string> CombinedPermutations(string digits, int size)
    {
        var set = new HashSet<string>();
        void Comb(List<char> cur, int idx) {
            if (cur.Count == size) {
                foreach (var p in Permutations(new string(cur.ToArray())))
                    set.Add(p);
                return;
            }
            for (int i=idx;i<digits.Length;i++){
                cur.Add(digits[i]);
                Comb(cur,i+1);
                cur.RemoveAt(cur.Count-1);
            }
        }
        if (digits.All(char.IsDigit) && digits.Length >= size) Comb(new List<char>(), 0);
        return set;
    }

    public static int CountHits<T>(IEnumerable<T> chosen, IEnumerable<T> pool)
      => chosen.Distinct().Count(pool.Contains);

    public static int Comb(int n, int k)
    {
        if (k > n || k < 0) return 0;
        long r = 1;
        for (int i=1;i<=k;i++) r = r * (n - (k - i)) / i;
        return (int)r;
    }
}
