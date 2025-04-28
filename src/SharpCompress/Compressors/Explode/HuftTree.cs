/*
 * This code has been converted to C# based on the original huft_tree code found in
 * inflate.c -- by Mark Adler version c17e, 30 Mar 2007
 */

namespace SharpCompress.Compressors.Explode;

public class huftNode
{
    public int NumberOfExtraBits; /* number of extra bits or operation */
    public int NumberOfBitsUsed; /* number of bits in this code or subcode */
    public int Value; /* literal, length base, or distance base */
    public huftNode[] ChildNodes = []; /* next level of table */
}

public static class HuftTree
{
    private const int INVALID_CODE = 99;

    /* If BMAX needs to be larger than 16, then h and x[] should be ulg. */
    private const int BMAX = 16; /* maximum bit length of any code (16 for explode) */
    private const int N_MAX = 288; /* maximum number of codes in any set */

    public static int huftbuid(
        int[] arrBitLengthForCodes,
        int numberOfCodes,
        int numberOfSimpleValueCodes,
        int[] arrBaseValuesForNonSimpleCodes,
        int[] arrExtraBitsForNonSimpleCodes,
        out huftNode[] outHufTable,
        ref int outBitsForTable
    )
    /* Given a list of code lengths and a maximum table size, make a set of
    tables to decode that set of codes. Return zero on success, one if
    the given code set is incomplete (the tables are still built in this
    case), two if the input is invalid (all zero length codes or an
    oversubscribed set of lengths), and three if not enough memory.
    The code with value 256 is special, and the tables are constructed
    so that no bits beyond that code are fetched when that code is
    decoded. */
    {
        outHufTable = [];

        /* Generate counts for each bit length */
        int lengthOfEOBcode = numberOfCodes > 256 ? arrBitLengthForCodes[256] : BMAX; /* set length of EOB code, if any */

        int[] arrBitLengthCount = new int[BMAX + 1];
        for (int i = 0; i < BMAX + 1; i++)
            arrBitLengthCount[i] = 0;

        int pIndex = 0;
        int counterCurrentCode = numberOfCodes;
        do
        {
            arrBitLengthCount[arrBitLengthForCodes[pIndex]]++;
            pIndex++; /* assume all entries <= BMAX */
        } while ((--counterCurrentCode) != 0);

        if (arrBitLengthCount[0] == numberOfCodes) /* null input--all zero length codes */
        {
            return 0;
        }

        /* Find minimum and maximum length, bound *outBitsForTable by those */
        int counter;
        for (counter = 1; counter <= BMAX; counter++)
            if (arrBitLengthCount[counter] != 0)
                break;

        int numberOfBitsInCurrentCode = counter; /* minimum code length */
        if (outBitsForTable < counter)
            outBitsForTable = counter;

        for (counterCurrentCode = BMAX; counterCurrentCode != 0; counterCurrentCode--)
            if (arrBitLengthCount[counterCurrentCode] != 0)
                break;

        int maximumCodeLength = counterCurrentCode; /* maximum code length */
        if (outBitsForTable > counterCurrentCode)
            outBitsForTable = counterCurrentCode;

        /* Adjust last length count to fill out codes, if needed */
        int numberOfDummyCodesAdded;
        for (
            numberOfDummyCodesAdded = 1 << counter;
            counter < counterCurrentCode;
            counter++, numberOfDummyCodesAdded <<= 1
        )
            if ((numberOfDummyCodesAdded -= arrBitLengthCount[counter]) < 0)
                return 2; /* bad input: more codes than bits */

        if ((numberOfDummyCodesAdded -= arrBitLengthCount[counterCurrentCode]) < 0)
            return 2;

        arrBitLengthCount[counterCurrentCode] += numberOfDummyCodesAdded;

        /* Generate starting offsets into the value table for each length */
        int[] bitOffset = new int[BMAX + 1];
        bitOffset[1] = 0;
        counter = 0;
        pIndex = 1;
        int xIndex = 2;
        while ((--counterCurrentCode) != 0)
        { /* note that i == g from above */
            bitOffset[xIndex++] = (counter += arrBitLengthCount[pIndex++]);
        }

        /* Make a table of values in order of bit lengths */
        int[] arrValuesInOrderOfBitLength = new int[N_MAX];
        for (int i = 0; i < N_MAX; i++)
            arrValuesInOrderOfBitLength[i] = 0;

        pIndex = 0;
        counterCurrentCode = 0;
        do
        {
            if ((counter = arrBitLengthForCodes[pIndex++]) != 0)
                arrValuesInOrderOfBitLength[bitOffset[counter]++] = counterCurrentCode;
        } while (++counterCurrentCode < numberOfCodes);

        numberOfCodes = bitOffset[maximumCodeLength]; /* set numberOfCodes to length of v */

        /* Generate the Huffman codes and for each, make the table entries */
        bitOffset[0] = counterCurrentCode = 0; /* first Huffman code is zero */
        pIndex = 0; /* grab values in bit order */
        int tableLevel = -1; /* no tables yet--level -1 */
        int bitsBeforeThisTable = 0;
        int[] arrLX = new int[BMAX + 1];
        int stackOfBitsPerTable = 1; /* stack of bits per table */
        arrLX[stackOfBitsPerTable - 1] = 0; /* no bits decoded yet */

        huftNode[][] arrHufTableStack = new huftNode[BMAX][];
        huftNode[] pointerToCurrentTable = [];
        int numberOfEntriesInCurrentTable = 0;

        bool first = true;

        /* go through the bit lengths (k already is bits in shortest code) */
        for (; numberOfBitsInCurrentCode <= maximumCodeLength; numberOfBitsInCurrentCode++)
        {
            int counterForCodes = arrBitLengthCount[numberOfBitsInCurrentCode];
            while ((counterForCodes--) != 0)
            {
                /* here i is the Huffman code of length k bits for value *p */
                /* make tables up to required level */
                while (
                    numberOfBitsInCurrentCode
                    > bitsBeforeThisTable + arrLX[stackOfBitsPerTable + tableLevel]
                )
                {
                    bitsBeforeThisTable += arrLX[stackOfBitsPerTable + (tableLevel++)]; /* add bits already decoded */

                    /* compute minimum size table less than or equal to *outBitsForTable bits */
                    numberOfEntriesInCurrentTable =
                        (numberOfEntriesInCurrentTable = maximumCodeLength - bitsBeforeThisTable)
                        > outBitsForTable
                            ? outBitsForTable
                            : numberOfEntriesInCurrentTable; /* upper limit */
                    int fBitCounter1 =
                        1 << (counter = numberOfBitsInCurrentCode - bitsBeforeThisTable);
                    if (fBitCounter1 > counterForCodes + 1) /* try a k-w bit table */
                    { /* too few codes for k-w bit table */
                        fBitCounter1 -= counterForCodes + 1; /* deduct codes from patterns left */
                        xIndex = numberOfBitsInCurrentCode;
                        while (++counter < numberOfEntriesInCurrentTable) /* try smaller tables up to z bits */
                        {
                            if ((fBitCounter1 <<= 1) <= arrBitLengthCount[++xIndex])
                                break; /* enough codes to use up j bits */
                            fBitCounter1 -= arrBitLengthCount[xIndex]; /* else deduct codes from patterns */
                        }
                    }
                    if (
                        bitsBeforeThisTable + counter > lengthOfEOBcode
                        && bitsBeforeThisTable < lengthOfEOBcode
                    )
                        counter = lengthOfEOBcode - bitsBeforeThisTable; /* make EOB code end at table */

                    numberOfEntriesInCurrentTable = 1 << counter; /* table entries for j-bit table */
                    arrLX[stackOfBitsPerTable + tableLevel] = counter; /* set table size in stack */

                    /* allocate and link in new table */
                    pointerToCurrentTable = new huftNode[numberOfEntriesInCurrentTable];

                    // set the pointer, pointed to by *outHufTable to the second huft in pointertoCurrentTable
                    if (first)
                    {
                        outHufTable = pointerToCurrentTable; /* link to list for huft_free() */
                        first = false;
                    }

                    arrHufTableStack[tableLevel] = pointerToCurrentTable; /* table starts after link */

                    /* connect to last table, if there is one */
                    if (tableLevel != 0)
                    {
                        bitOffset[tableLevel] = counterCurrentCode; /* save pattern for backing up */

                        huftNode vHuft = new huftNode
                        {
                            NumberOfBitsUsed = arrLX[stackOfBitsPerTable + tableLevel - 1], /* bits to dump before this table */
                            NumberOfExtraBits = 32 + counter, /* bits in this table */
                            ChildNodes = pointerToCurrentTable, /* pointer to this table */
                        };

                        counter =
                            (counterCurrentCode & ((1 << bitsBeforeThisTable) - 1))
                            >> (bitsBeforeThisTable - arrLX[stackOfBitsPerTable + tableLevel - 1]);
                        arrHufTableStack[tableLevel - 1][counter] = vHuft; /* connect to last table */
                    }
                }

                /* set up table entry in r */
                huftNode vHuft1 = new huftNode
                {
                    NumberOfBitsUsed = numberOfBitsInCurrentCode - bitsBeforeThisTable,
                };

                if (pIndex >= numberOfCodes)
                    vHuft1.NumberOfExtraBits = INVALID_CODE; /* out of values--invalid code */
                else if (arrValuesInOrderOfBitLength[pIndex] < numberOfSimpleValueCodes)
                {
                    vHuft1.NumberOfExtraBits = (
                        arrValuesInOrderOfBitLength[pIndex] < 256 ? 32 : 31
                    ); /* 256 is end-of-block code */
                    vHuft1.Value = arrValuesInOrderOfBitLength[pIndex++]; /* simple code is just the value */
                }
                else
                {
                    vHuft1.NumberOfExtraBits = arrExtraBitsForNonSimpleCodes[
                        arrValuesInOrderOfBitLength[pIndex] - numberOfSimpleValueCodes
                    ]; /* non-simple--look up in lists */
                    vHuft1.Value = arrBaseValuesForNonSimpleCodes[
                        arrValuesInOrderOfBitLength[pIndex++] - numberOfSimpleValueCodes
                    ];
                }

                /* fill code-like entries with r */
                int fBitCounter2 = 1 << (numberOfBitsInCurrentCode - bitsBeforeThisTable);
                for (
                    counter = counterCurrentCode >> bitsBeforeThisTable;
                    counter < numberOfEntriesInCurrentTable;
                    counter += fBitCounter2
                )
                    pointerToCurrentTable[counter] = vHuft1;

                /* backwards increment the k-bit code i */
                for (
                    counter = 1 << (numberOfBitsInCurrentCode - 1);
                    (counterCurrentCode & counter) != 0;
                    counter >>= 1
                )
                    counterCurrentCode ^= counter;
                counterCurrentCode ^= counter;

                /* backup over finished tables */
                while (
                    (counterCurrentCode & ((1 << bitsBeforeThisTable) - 1)) != bitOffset[tableLevel]
                )
                    bitsBeforeThisTable -= arrLX[stackOfBitsPerTable + (--tableLevel)];
            }
        }

        /* return actual size of base table */
        outBitsForTable = arrLX[stackOfBitsPerTable];

        /* Return true (1) if we were given an incomplete table */
        return (numberOfDummyCodesAdded != 0 && maximumCodeLength != 1) ? 1 : 0;
    }
}
