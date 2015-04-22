﻿using CSharpSupport.Attributes;
using CSharpSupport.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace CSharpSupport.Implementations
{
    /// <summary>
    /// Instances of this class should be used only by a single request and so is not written to be thread safe. This is partly because the SETERROR and
    /// CLEARANYERROR methods have no explicit way to be associated with a specific request (which is not a problem if each instance is associated with
    /// one specific request) but also so that it can be explicitly disposed after each request completes to ensure that any unmanaged resources are
    /// cleaned up. VBScript's deterministic garbage collector can tidy up more aggressively, relying upon reference counting, the best that we can do
    /// with the C# code is for this to implement IDiposable and to ensure that everything is tidy when the request completes and Dispose is called.
    /// </summary>
    public class DefaultRuntimeFunctionalityProvider : IProvideVBScriptCompatFunctionalityToIndividualRequests
    {
        private readonly IAccessValuesUsingVBScriptRules _valueRetriever;
        private readonly List<IDisposable> _disposableReferencesToClearAfterTheRequest;
        private readonly Queue<int> _availableErrorTokens;
        private readonly Dictionary<int, ErrorTokenState> _activeErrorTokens;
        private Exception _trappedErrorIfAny;
        public DefaultRuntimeFunctionalityProvider(Func<string, string> nameRewriter, IAccessValuesUsingVBScriptRules valueRetriever)
        {
            if (valueRetriever == null)
                throw new ArgumentNullException("valueRetriever");

            _valueRetriever = valueRetriever;
            _disposableReferencesToClearAfterTheRequest = new List<IDisposable>();
            _availableErrorTokens = new Queue<int>();
            _activeErrorTokens = new Dictionary<int, ErrorTokenState>();
            _trappedErrorIfAny = null;
        }

        private enum ErrorTokenState
        {
            OnErrorResumeNext,
            OnErrorGoto0
        }

        ~DefaultRuntimeFunctionalityProvider()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var disposableResource in _disposableReferencesToClearAfterTheRequest)
                {
                    try { disposableResource.Dispose(); }
                    catch { }
                }
            }
        }

        // Arithemetic operators
        public double POW(object l, object r) { throw new NotImplementedException(); }
        public double DIV(object l, object r) { throw new NotImplementedException(); }
        public double MULT(object l, object r) { throw new NotImplementedException(); }
        public int INTDIV(object l, object r) { throw new NotImplementedException(); }
        public double MOD(object l, object r) { throw new NotImplementedException(); }
        public object ADD(object l, object r)
        {
            // See https://msdn.microsoft.com/en-us/library/kd1e4aey(v=vs.84).aspx
            l = VAL(l);
            r = VAL(r);
            if ((l == DBNull.Value) || (r == DBNull.Value))
                return DBNull.Value;
            if ((l == null) && (r == null))
                return (Int16)0;
            else if ((l == null) || (r == null))
                return l ?? r;
            if ((l is string) && (r is string))
                return (string)l + (string)r;

            // TODO: This just covers some simple cases, it is not the full implementation yet
            l = NUM(l, r);
            r = NUM(r, l);
            if ((l is short) && (r is short))
            {
                var shortL = (short)l;
                var shortR = (short)r;
                if (shortR >= 0)
                {
                    if (shortL <= short.MaxValue - shortR)
                        return (short)(shortL + shortR);
                    return shortL + shortR; // Note: short + short will return int so we don't need any casts here
                }
            }

            /*
            // TODO: Need to move into new data type if would overflow the current one

            // Now we need to treat both as numbers. We need them both to be a consistent type so that we can perform the addition. Using the
            // numericValuesTheTypeMustBeAbleToContain method signature of NUM will allow us  to do that, but we'll still have to inspect the
            // types to add correctly (NUM will only return VBScript-supported numeric types, though, which will make this easier).
            l = NUM(l, r);
            r = NUM(r, l);
            if (l is DateTime)
            {
                var dateResult = ((DateTime)l).AddDays(DateToDouble((DateTime)r));
                if ((dateResult < VBScriptConstants.EarliestPossibleDate) || (dateResult > VBScriptConstants.LatestPossibleDate))
                    throw new VBScriptOverflowException();
                return dateResult;
            }
            else if (l is decimal)
            {
                var decimalL = (decimal)l;
                var decimalR = (decimal)r;
                if ((decimalR > 0) && (VBScriptConstants.MaxCurrencyValue - decimalR > decimalL)) // TODO: Ensure include test coverage for this!
                    throw new VBScriptOverflowException();
                else if ((decimalR < 0) && (decimalL < VBScriptConstants.MinCurrencyValue - decimalR)) // TODO: Ensure include test coverage for this!
                    throw new VBScriptOverflowException();
                return decimalL + decimalR;
            }
            //return NUM(l) + NUM(r);
             */
            throw new NotImplementedException(); // TODO
        }
        public double SUBT(object o) { throw new NotImplementedException(); }
        public double SUBT(object l, object r) { throw new NotImplementedException(); }

        // String concatenation
        public object CONCAT(object l, object r)
        {
            // See https://msdn.microsoft.com/en-us/library/sx97884w(v=vs.84).aspx
            l = VAL(l);
            r = VAL(r);
            if ((l == DBNull.Value) && (r == DBNull.Value))
                return DBNull.Value;
            return ((l == null) ? "" : l.ToString()) + ((r == null) ? "" : r.ToString());
        }
        
        /// <summary>
        /// This may never be called with less than two values (otherwise an exception will be thrown)
        /// </summary>
        public object CONCAT(params object[] values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            if (values.Length < 2)
                throw new ArgumentException("There must be at least two values specified for the CONCAT operation");

            // Concatenate the first two values (using the standard two-value version of the method) and then concatenate each further values on to
            // this accumulator. This could very likely be done in a more efficient manner by recursively splitting the array of values but this will
            // do for now.
            var combinedValue = CONCAT(values[0], values[1]);
            foreach (var additionalValue in values.Skip(2))
                combinedValue = CONCAT(combinedValue, additionalValue);
            return combinedValue;
        }

        // Logical operators (these return VBScript Null if one or both sides of the comparison are VBScript Null)
        public object NOT(object o) { throw new NotImplementedException(); }
        public object AND(object l, object r) { throw new NotImplementedException(); }
        public object OR(object l, object r) { throw new NotImplementedException(); }
        public object XOR(object l, object r) { throw new NotImplementedException(); }

        // Comparison operators (these return VBScript Null if one or both sides of the comparison are VBScript Null)
        /// <summary>
        /// This will return DBNull.Value or boolean value. VBScript has rules about comparisons between "hard-typed" values (aka literals), such
        /// that a comparison between (a = 1) requires that the value "a" be parsed into a numeric value (resulting in a Type Mismatch if this is
        /// not possible). However, this logic must be handled by the translation process before the EQ method is called. Both comparison values
        /// must be treated as non-object-references, so if they are not when passed in then the method will try to retrieve non-object values
        /// from them - if this fails then a Type Mismatch error will be raised. If there are no issues in preparing both comparison values,
        /// this will return DBNull.Value if either value is DBNull.Value and a boolean otherwise.
        /// </summary>
        public object EQ(object l, object r) { return ToVBScriptNullable(EQ_Internal(l, r)); }
        private bool? EQ_Internal(object l, object r)
        {
            // Both sides of the comparison must be simple VBScript values (ie. not object references) - pushing both values through VAL will handle
            // that (an exception will be raised if this operation fails and the value will not be affect if it was already an acceptable type)
            l = VAL(l);
            r = VAL(r);
            
            // Let's get the outliers out of the way; VBScript Null and Empty..
            if ((l == DBNull.Value) || (r == DBNull.Value))
                return null; // If one or both sides of the comparison are "Null" then this is what is returned
            if ((l == null) && (r == null))
                return true; // If both sides are Empty then they are considered to match
            else if ((l == null) || (r == null))
            {
                // The default values of VBScript primitives (number, strings and booleans) are considered to match Empty
                var nonNullValue = l ?? r;
                if ((IsDotNetNumericType(nonNullValue) && (Convert.ToDouble(nonNullValue)) == 0)
                || ((nonNullValue as string) == "")
                || ((nonNullValue is bool) && !(bool)nonNullValue))
                    return true;
                return false;
            }

            // Booleans have some funny behaviour in that they will match values of other types (numbers, but not strings unless string literals
            // are in the comparison, which is not logic that this method has to deal with). If one of the values is a boolean and the other isn't,
            // and none of the special cases are met, then there must not be a match.
            if ((l is bool) && (r is bool))
                return (bool)l == (bool)r;
            else if ((l is bool) || (r is bool))
            {
                var boolValue = (bool)((l is bool) ? l : r);
                var nonBoolValue = (l is bool) ? r : l;
                if (!IsDotNetNumericType(nonBoolValue))
                    return false;
                return (boolValue && (Convert.ToDouble(nonBoolValue) == -1)) || (!boolValue && (Convert.ToDouble(nonBoolValue) == 0));
            }

            // Now consider numbers on one or both sides - all special cases are out of the way now so they're either equal or they're not (both
            // sides must be numbers, otherwise it's a non-match)
            if (IsDotNetNumericType(l) && IsDotNetNumericType(r))
                return Convert.ToDouble(l) == Convert.ToDouble(r);
            else if (IsDotNetNumericType(l) || IsDotNetNumericType(r))
                return false;

            // Now do the same for strings and then dates - same deal; they must have consistent types AND values
            if ((l is string) && (r is string))
                return (string)l == (string)r;
            else if ((l is string) || (r is string))
                return false;
            if ((l is DateTime) && (r is DateTime))
                return (DateTime)l == (DateTime)r;

            // Frankly, if we get here then I have no idea what's happened. It will be much easier to identify issues (if any are encountered) if an
            // exception is raised rather than a false response return
            throw new NotSupportedException("Don't know how to compare values of type " + TYPENAME(l) + " and " + TYPENAME(r));
        }

        public object NOTEQ(object l, object r)
        {
            // We can just reverse EQ_Internal's result here, unless it returns null - if it returns null then it means that comparison was not
            // meaningful (one or both sides were DBNull.Value) and so DBNull.Value should be returned.
            var opposingEqualityResult = EQ_Internal(l, r);
            if (opposingEqualityResult == null)
                return null;
            return !opposingEqualityResult.Value;
        }

        public object LT(object l, object r) { return ToVBScriptNullable(LT_Internal(l, r, allowEquals: false)); }
        public object LTE(object l, object r) { return ToVBScriptNullable(LT_Internal(l, r, allowEquals: true)); }
        /// <summary>
        /// This takes the logic from LT but throws an exception if a DBNull.Value is taken as part of the comparison (which is how it is able to
        /// return a boolean, rather than an object - which LT has to since it may return a boolean OR DBNull.Value)
        /// </summary>
        public bool StrictLT(object l, object r)
        {
            var result = LT_Internal(l, r, allowEquals: false);
            if (result == null)
                throw new InvalidUseOfNullException();
            return result.Value;
        }
        /// <summary>
        /// This takes the logic from LTE but throws an exception if a DBNull.Value is taken as part of the comparison (which is how it is able to
        /// return a boolean, rather than an object - which LTE has to since it may return a boolean OR DBNull.Value)
        /// </summary>
        public bool StrictLTE(object l, object r)
        {
            var result = LT_Internal(l, r, allowEquals: true);
            if (result == null)
                throw new InvalidUseOfNullException();
            return result.Value;
        }
        private bool? LT_Internal(object l, object r, bool allowEquals)
        {
            // Both sides of the comparison must be simple VBScript values (ie. not object references) - pushing both values through VAL will handle
            // that (an exception will be raised if this operation fails and the value will not be affect if it was already an acceptable type)
            l = VAL(l);
            r = VAL(r);

            // If one or both sides of the comparison as VBScript Null then that is what is returned
            if ((l == DBNull.Value) || (r == DBNull.Value))
                return null;

            // Check the equality case first, since there may be an early exit we can make (this should return a true or false since the "Null" cases
            // have been handled) - if the values ARE equal then either return true (if allowEquals is true) or false (if allowEquals is false). If
            // not then we'll have to do more work.
            var eq = EQ_Internal(l, r);
            if (eq == null)
                throw new NotSupportedException("Don't know how to compare values of type " + TYPENAME(l) + " and " + TYPENAME(r));
            if (eq.Value)
                return allowEquals;

            // Deal with string special cases next - if both are strings then perform a string comparison. If only one is a string, and it is not blank,
            // then that value is bigger (so if it's on the left then return false and if it's on the right then return true). Blank strings get special
            // handling and are effectively treated as zero (see further down).
            var lString = l as string;
            var rString = r as string;
            if ((lString != null) && (rString != null))
            {
                var stringComparisonResult = STRCOMP_Internal(lString, rString, 0);
                if ((stringComparisonResult == null) || (stringComparisonResult.Value == 0))
                    throw new NotSupportedException("Don't know how to compare values of type " + TYPENAME(l) + " and " + TYPENAME(r));
                return stringComparisonResult.Value < 0;
            }
            if ((lString != null) && (lString != ""))
                return false;
            if ((rString != null) && (rString != ""))
                return true;

            // Now we should only have values which can treated as numeric
            // - Actual numbers
            // - Booleans (which return zero or minus one when passed through CDBL)
            // - Null aka VBScript Empty (which returns zero when passed through CDBL)
            // - Blank strings (which can not be passed through CDBL without causing an error, but which we can treat as zero)
            var lNumeric = (lString == "") ? 0 : CDBL(l);
            var rNumeric = (rString == "") ? 0 : CDBL(r);
            return lNumeric < rNumeric;
        }

        public object GT(object l, object r) { return ToVBScriptNullable(GT_Internal(l, r, allowEquals: false)); }
        public object GTE(object l, object r) { return ToVBScriptNullable(GT_Internal(l, r, allowEquals: true)); }
        /// <summary>
        /// This takes the logic from GT but throws an exception if a DBNull.Value is taken as part of the comparison (which is how it is able to
        /// return a boolean, rather than an object - which GT has to since it may return a boolean OR DBNull.Value)
        /// </summary>
        public bool StrictGT(object l, object r)
        {
            var result = GT_Internal(l, r, allowEquals: false);
            if (result == null)
                throw new InvalidUseOfNullException();
            return result.Value;
        }
        /// <summary>
        /// This takes the logic from GTE but throws an exception if a DBNull.Value is taken as part of the comparison (which is how it is able to
        /// return a boolean, rather than an object - which GTE has to since it may return a boolean OR DBNull.Value)
        /// </summary>
        public bool StrictGTE(object l, object r)
        {
            var result = GT_Internal(l, r, allowEquals: true);
            if (result == null)
                throw new InvalidUseOfNullException();
            return result.Value;
        }
        private bool? GT_Internal(object l, object r, bool allowEquals)
        {
            // This can just LT_Internal, rather than trying to deal with too much logic itself. When calling LT_Internal, the "allowEquals" value must be
            // the opposite of what we have here - if we are considering GTE then we want !LT (since the equality case should be a match here and not a
            // result which is inverted), if we are considering GT here then we want !LTE (since then equality case would not be a match and LTE would
            // return true for equal l and r values and we would want to invert that result). If LT_Internal returns null, then it means that the
            // comparison is not meaningful (in other words, DBNull.Value was on one or both sides and so DBNull.Value should be returned for
            // any comparison - whether EQ, NOTEQ, LT, GT, etc..)
            var opposingLessThanResult = LT_Internal(l, r, !allowEquals);
            if (opposingLessThanResult == null)
                return null;
            return !opposingLessThanResult.Value;
        }

        public object IS(object l, object r) { throw new NotImplementedException(); }
        public object EQV(object l, object r) { throw new NotImplementedException(); }
        public object IMP(object l, object r) { throw new NotImplementedException(); }

        // Builtin functions - TODO: These are not fully specified yet (eg. LEFT requires more than one parameter and INSTR requires multiple parameters and
        // overloads to deal with optional parameters)
        // - Type conversions
        public byte CBYTE(object value) { return GetAsNumber<byte>(value, Convert.ToByte); }
        public object CBOOL(object value) { throw new NotImplementedException(); }
        public decimal CCUR(object value) { return GetAsNumber<decimal>(value, Convert.ToDecimal); }
        public double CDBL(object value) { return GetAsNumber<double>(value, Convert.ToDouble); }
        public object CDATE(object value) { throw new NotImplementedException(); }
        public Int16 CINT(object value) { return GetAsNumber<Int16>(value, Convert.ToInt16); }
        public int CLNG(object value) { return GetAsNumber<int>(value, Convert.ToInt32); }
        public float CSNG(object value) { return GetAsNumber<float>(value, Convert.ToSingle); }
        public string CSTR(object value) { throw new NotImplementedException(); }
        public string INT(object value) { throw new NotImplementedException(); }
        public string STRING(object value) { throw new NotImplementedException(); }
        // - Number functions
        public object ABS(object value) { throw new NotImplementedException(); }
        public object ATN(object value) { throw new NotImplementedException(); }
        public object COS(object value) { throw new NotImplementedException(); }
        public object EXP(object value) { throw new NotImplementedException(); }
        public object FIX(object value) { throw new NotImplementedException(); }
        public object LOG(object value) { throw new NotImplementedException(); }
        public object OCT(object value) { throw new NotImplementedException(); }
        public object RND(object value) { throw new NotImplementedException(); }
        public object ROUND(object value) { throw new NotImplementedException(); }
        public object SGN(object value) { throw new NotImplementedException(); }
        public object SIN(object value) { throw new NotImplementedException(); }
        public object SQR(object value) { throw new NotImplementedException(); }
        public object TAN(object value) { throw new NotImplementedException(); }
        // - String functions
        public object ASC(object value) { throw new NotImplementedException(); }
        public object ASCB(object value) { throw new NotImplementedException(); }
        public object ASCW(object value) { throw new NotImplementedException(); }
        public object CHR(object value) { throw new NotImplementedException(); }
        public object CHRB(object value) { throw new NotImplementedException(); }
        public object CHRW(object value) { throw new NotImplementedException(); }
        public object FILTER(object value) { throw new NotImplementedException(); }
        public object FORMATCURRENCY(object value) { throw new NotImplementedException(); }
        public object FORMATDATETIME(object value) { throw new NotImplementedException(); }
        public object FORMATNUMBER(object value) { throw new NotImplementedException(); }
        public object FORMATPERCENT(object value) { throw new NotImplementedException(); }
        public object HEX(object value) { throw new NotImplementedException(); }
        public object INSTR(object value) { throw new NotImplementedException(); }
        public object INSTRREV(object value) { throw new NotImplementedException(); }
        public object MID(object value) { throw new NotImplementedException(); }
        public object LEN(object value) { throw new NotImplementedException(); }
        public object LENB(object value) { throw new NotImplementedException(); }
        public object LEFT(object value, object maxLength)
        {
            // Validate inputs first
            value = VAL(value);
            var maxLengthInt = CLNG(maxLength);
            if (maxLengthInt < 0)
                throw new ArgumentOutOfRangeException("Invalid procedure call or argument: 'LEFT' (maxLength may not be a negative value)");

            // Deal with special cases
            if (value == null)
                return "";
            if (value == DBNull.Value)
                return DBNull.Value;

            var valueString = value.ToString();
            maxLengthInt = Math.Min(valueString.Length, maxLengthInt);
            return valueString.Substring(0, maxLengthInt);
        }
        public object LEFTB(object value, object maxLength) { throw new NotImplementedException(); }
        public object RGB(object value) { throw new NotImplementedException(); }
        public object RIGHT(object value, object maxLength)
        {
            // Validate inputs first
            value = VAL(value);
            var maxLengthInt = CLNG(maxLength);
            if (maxLengthInt < 0)
                throw new ArgumentOutOfRangeException("Invalid procedure call or argument: 'LEFT' (maxLength may not be a negative value)");

            // Deal with special cases
            if (value == null)
                return "";
            if (value == DBNull.Value)
                return DBNull.Value;

            var valueString = value.ToString();
            maxLengthInt = Math.Min(valueString.Length, maxLengthInt);
            return valueString.Substring(valueString.Length - maxLengthInt);
        }
        public object RIGHTB(object value, object maxLength) { throw new NotImplementedException(); }
        public object REPLACE(object value) { throw new NotImplementedException(); }
        public object SPACE(object value) { throw new NotImplementedException(); }
        public object SPLIT(object value) { throw new NotImplementedException(); }
        public object STRCOMP(object string1, object string2) { return STRCOMP(string1, string2, 0); }
        public object STRCOMP(object string1, object string2, object compare) { return ToVBScriptNullable<int>(STRCOMP_Internal(string1, string2, compare)); }
        private int? STRCOMP_Internal(object string1, object string2, object compare)
        {
            throw new NotImplementedException();
        }
        public object STRREVERSE(object value) { throw new NotImplementedException(); }
        public object TRIM(object value)
        {
            value = VAL(value);
            if (value == null)
                return "";
            else if (value == DBNull.Value)
                return DBNull.Value;
            return value.ToString().Trim(' ');
        }
        public object LTRIM(object value)
        {
            value = VAL(value);
            if (value == null)
                return "";
            else if (value == DBNull.Value)
                return DBNull.Value;
            return value.ToString().TrimStart(' ');
        }
        public object RTRIM(object value)
        {
            value = VAL(value);
            if (value == null)
                return "";
            else if (value == DBNull.Value)
                return DBNull.Value;
            return value.ToString().TrimEnd(' ');
        }
        public object LCASE(object value) { throw new NotImplementedException(); }
        public object UCASE(object value) { throw new NotImplementedException(); }
        // - Type comparisons
        public object ISARRAY(object value) { throw new NotImplementedException(); }
        public object ISDATE(object value) { throw new NotImplementedException(); }
        public object ISEMPTY(object value) { throw new NotImplementedException(); }
        public object ISNULL(object value) { throw new NotImplementedException(); }
        public object ISNUMERIC(object value) { throw new NotImplementedException(); }
        public object ISOBJECT(object value) { throw new NotImplementedException(); }
        public object TYPENAME(object value)
        {
            if (value == null)
                return "Null";
            if (value == DBNull.Value)
                return "Empty";
            if (IsVBScriptNothing(value))
                return "Nothing";
            var type = value.GetType();
            var sourceClassName = type.GetCustomAttributes(typeof(SourceClassName), inherit: true).FirstOrDefault() as SourceClassName;
            if (sourceClassName != null)
                return sourceClassName.Name;

            // TODO: This needs to deal with numeric types much better - eg. "Int16" => "Integer" - have they all been covered now?
            if (type == typeof(bool))
                return "Boolean";
            if (type == typeof(byte))
                return "Byte";
            if (type == typeof(Int16))
                return "Integer";
            if (type == typeof(Int32))
                return "Long";
            if (type == typeof(double))
                return "Double";
            if (type == typeof(DateTime))
                return "Date";
            if (type == typeof(Decimal))
                return "Currency";

            // TODO: Does this deal with COM objects such as "Recordset" or will it show "_COMObject" (or whatever)
            // - If ComVisible then step through the type inheritance tree for the first ComVisible(true) and use that class' Type Name?
            return value.GetType().Name;
        }
        public object VARTYPE(object value) { throw new NotImplementedException(); }
        // - Array functions
        public object ARRAY(object value) { throw new NotImplementedException(); }
        public object ERASE(object value) { throw new NotImplementedException(); }
        public object JOIN(object value) { throw new NotImplementedException(); }
        public object LBOUND(object value) { throw new NotImplementedException(); }
        public object UBOUND(object value) { throw new NotImplementedException(); }
        // - Date functions
        public DateTime NOW() { return DateTime.Now; }
        public DateTime DATE() { return DateTime.Now.Date; }
        public DateTime TIME() { return new DateTime(DateTime.Now.TimeOfDay.Ticks); }
        public object DATEADD(object value) { throw new NotImplementedException(); }
        public object DATEDIFF(object value) { throw new NotImplementedException(); }
        public object DATEPART(object value) { throw new NotImplementedException(); }
        public object DATESERIAL(object year, object month, object date) { throw new NotImplementedException(); }
        public object DATEVALUE(object value) { throw new NotImplementedException(); }
        public object TIMESERIAL(object value) { throw new NotImplementedException(); }
        public object TIMEVALUE(object value) { throw new NotImplementedException(); }
        public object NOW(object value) { throw new NotImplementedException(); }
        public object DAY(object value) { throw new NotImplementedException(); }
        public object MONTH(object value) { throw new NotImplementedException(); }
        public object MONTHNAME(object value) { throw new NotImplementedException(); }
        public object YEAR(object value) { throw new NotImplementedException(); }
        public object WEEKDAY(object value) { throw new NotImplementedException(); }
        public object WEEKDAYNAME(object value) { throw new NotImplementedException(); }
        public object HOUR(object value) { throw new NotImplementedException(); }
        public object MINUTE(object value) { throw new NotImplementedException(); }
        public object SECOND(object value) { throw new NotImplementedException(); }
        // - Object creation
        public object CREATEOBJECT(object value) { throw new NotImplementedException(); }
        public object GETOBJECT(object value) { throw new NotImplementedException(); }
        public object EVAL(object value) { throw new NotImplementedException(); }
        public object EXECUTE(object value) { throw new NotImplementedException(); }
        public object EXECUTEGLOBAL(object value) { throw new NotImplementedException(); }
        // - Misc
        public object GETLOCALE(object value) { throw new NotImplementedException(); }
        public object GETREF(object value) { throw new NotImplementedException(); }
        public object INPUTBOX(object value) { throw new NotImplementedException(); }
        public object LOADPICTURE(object value) { throw new NotImplementedException(); }
        public object MSGBOX(object value) { throw new NotImplementedException(); }
        public string SCRIPTENGINE(object value) { throw new NotImplementedException(); }
        public int SCRIPTENGINEBUILDVERSION(object value) { throw new NotImplementedException(); }
        public int SCRIPTENGINEMAJORVERSION(object value) { throw new NotImplementedException(); }
        public int SCRIPTENGINEMINORVERSION(object value) { throw new NotImplementedException(); }
        public object SETLOCALE(object value) { throw new NotImplementedException(); }

        /// <summary>
        /// This returns the value without any immediate processing, but may keep a reference to it and dispose of it (where applicable) after
        /// the request completes (to try to avoid resources from not being cleaned up in the absence of the VBScript deterministic garbage
        /// collection - classes with a Class_Terminate function are translated into IDisposable types and, while IDisposable.Dispose will not
        /// be called by the translated code, it may be called after the request ends if the requests are tracked here. This will throw an
        /// exception for a null value.
        /// </summary>
        public object NEW(object value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            var disposableResource = value as IDisposable;
            if (disposableResource != null)
                _disposableReferencesToClearAfterTheRequest.Add(disposableResource);
            return value;
        }

        // Array definitions
        public void NEWARRAY(IEnumerable<object> dimensions, Action<object> targetSetter)
        {
            throw new NotImplementedException(); // TODO
        }

        public void RESIZEARRAY(object array, IEnumerable<object> dimensions, Action<object> targetSetter)
        {
            throw new NotImplementedException(); // TODO
        }

        private IEnumerable<int> GetDimensions(IEnumerable<object> dimensions)
        {
            if (dimensions == null)
                throw new ArgumentNullException("dimensions");

            throw new NotImplementedException(); // TODO
        }

        public object NEWREGEXP() { throw new NotImplementedException("NEWREGEXP not yet implemented"); } // TODO

        // TODO: Consider using error translations from http://blogs.msdn.com/b/ericlippert/archive/2004/08/25/error-handling-in-vbscript-part-three.aspx
        public object ERR { get { throw new NotImplementedException("ERR not implemented yet"); } } // TODO

        /// <summary>
        /// There are some occassions when the translated code needs to throw a runtime exception based on the content of the source code - eg.
        ///   WScript.Echo 1()
        /// It is clear that "1" is a numeric constant and not a function, and so may not be called as one. However, this is not invalid VBScript and so is
        /// not a compile time error, it is something that must result in an exception at runtime. In these cases, where it is known at the time of translation
        /// that an exception must be thrown, this method may be used to do so at runtime. This is different to SETERROR, since that records an exception that
        /// has already been thrown - this throws the specified exception.
        /// </summary>
        public void RAISEERROR(Exception e)
        {
            if (e == null)
                throw new ArgumentNullException("e");

            throw e;
        }

        public void SETERROR(Exception e)
        {
            // Note that there is (at most) only a single error associated with an executing request. If the error-trapping is enabled and a function F1()
            // executes code that raises an error but then goes and calls F2() which also raises an error, the error recorded from the code in F1 that
            // occured before calling F2 is lost, it is overwritten by F2. So there is no need to try to map trapped errors onto error tokens, there is
            // only one per request (or zero - if there has been no error trapped, or if there HAS been an error trapped that has then been cleared).
            if (e == null)
                throw new ArgumentNullException("e");
            _trappedErrorIfAny = e;
        }
        
        public void CLEARANYERROR()
        {
            // This should be called by translated code that originates from an ON ERROR GOTO 0 with no corresponding ON ERROR RESUME NEXT - the translation
            // process will not emit code to call GETERRORTRAPPINGTOKEN since the source is not trying to trap any errors. However, any error information
            // must be cleared nonetheless, since there was an ON ERROR GOTO 0 in the source. It will also be required when Err.Clear is called.
            _trappedErrorIfAny = null;
        }

        public int GETERRORTRAPPINGTOKEN()
        {
            // Every time error-trapping is enabled within a function (or the outermost scope, where code doesn't run within a function in VBScript), the
            // translated code must request an "error trapping token". This is used to keep track of where error-trapping is and isn't enabled. If, for
            // example, a function F1 includes an ON ERROR RESUME NEXT and then calls F2 which includes its own ON ERROR RESUME NEXT and then later an
            // ON ERROR GOTO 0, this must only disable error-trapping within F2, the error-trapping that was enabled in F1 must continue to be enabled.
            // It isn't known at translation time how many error tokens may be required since this depends upon how the code executes - if F2 calls
            // itself then within its ON ERROR RESUME NEXT .. ON ERROR GOTO 0 region, an ON ERROR GOTO 0 call from that second call to F2 must not
            // disable error-trapping in the context of the first F2 call. So error tokens need to be handled dynamically. To try to only maintain as
            // many as strictly necessary, there is a queue of available tokens that is used to service GETERRORTRAPPINGTOKEN calls - after an error
            // token is returned (through RELEASEERRORTRAPPINGTOKEN), it goes back into the queue to potentially be used again. If the queue is empty
            // when this method is called then a new token is created. The token values are incremented each time this happens to ensure that they are
            // unique. This is why it's important that tokens are properly released - either when error-trapping is disabled (through an explicit ON
            // ERROR GOTO 0 or through an error being trapped or through a function scope ending where ON ERROR RESUME NEXT was set).
            // Note: When tokens are first requested, they default to the "OnErrorGoto0" state - meaning that error-trapping is not enabled currently
            // for that token. Error-trapping is enabled through a subsequent call to STARTERRORTRAPPINGANDCLEARANYERROR.
            int token;
            if (_availableErrorTokens.Any())
                token = _availableErrorTokens.Dequeue();
            else
                token = _availableErrorTokens.Count + _availableErrorTokens.Count;
            _activeErrorTokens.Add(token, ErrorTokenState.OnErrorGoto0);
            return token;
        }

        public void RELEASEERRORTRAPPINGTOKEN(int errorToken)
        {
            if (!_activeErrorTokens.ContainsKey(errorToken))
                throw new Exception("This error token is not active - this indicates mismatched error token (de)registrations in the translated code");
            _activeErrorTokens.Remove(errorToken);
            _availableErrorTokens.Enqueue(errorToken);
        }

        public void STARTERRORTRAPPINGANDCLEARANYERROR(int errorToken)
        {
            // Note: Whenever error trapping is explicitly enabled or disabled, any error is cleared. If two methods are called within an OERN..
            //   ON ERROR RESUME Next
            //   F1()
            //   F2()
            // .. and F1() raises an error, that error's information will be maintained while F2 is called (if it is called without an error being
            // raised) unless F2 or any code it calls contains On Error Resume Next or On Error Goto - if this is the case then the error from F1
            // is lost forever. This is why _trappedErrorIfAny is set to null here and in STOPERRORTRAPPINGANDCLEARANYERROR.
            if (!_activeErrorTokens.ContainsKey(errorToken))
                throw new Exception("This error token is not active - this indicates mismatched error token (de)registrations in the translated code");
            _activeErrorTokens[errorToken] = ErrorTokenState.OnErrorResumeNext;
            _trappedErrorIfAny = null;
        }
        
        public void STOPERRORTRAPPINGANDCLEARANYERROR(int errorToken)
        {
            if (!_activeErrorTokens.ContainsKey(errorToken))
                throw new Exception("This error token is not active - this indicates mismatched error token (de)registrations in the translated code");
            _activeErrorTokens[errorToken] = ErrorTokenState.OnErrorGoto0;
            _trappedErrorIfAny = null;
        }

        public void HANDLEERROR(int errorToken, Action action)
        {
            if (!_activeErrorTokens.ContainsKey(errorToken))
                throw new Exception("This error token is not active - this indicates mismatched error token (de)registrations in the translated code");
            
            try
            {
                action();
            }
            catch(Exception e)
            {
                // Translated programs shouldn't provide any actions that register or unregister error tokens, but since we've just gone off and
                // attempted to do some unknown work, it's best to check
                if (!_activeErrorTokens.ContainsKey(errorToken))
                    throw new Exception("This error token is not active - this indicates mismatched error token (de)registrations in the translated code");

                if (_activeErrorTokens[errorToken] == ErrorTokenState.OnErrorResumeNext)
                    SETERROR(e);
                else
                {
                    RELEASEERRORTRAPPINGTOKEN(errorToken);
                    throw;
                }
            }
        }

        /// <summary>
        /// This layers error-handling on top of the IAccessValuesUsingVBScriptRules.IF method, if error-handling is enabled for the specified
        /// token then evaluation of the value will be attempted - if an error occurs then it will be recorded and the condition will be treated
        /// as true, since this is VBScript's behaviour. It will throw an exception for a null valueEvaluator or an invalid errorToken.
        /// </summary>
        public bool IF(Func<object> valueEvaluator, int errorToken)
        {
            if (valueEvaluator == null)
                throw new ArgumentNullException("valueEvaluator");
            
            // VBScript's behaviour is quite mad here; if error-trapping is enabled when an IF condition must be evaluated, and if that evaluation results in
            // and error being raised, then act as if the condition was met. So we default to true and then try to perform the evalaluation with HANDLEERROR.
            // If an error is thrown and error-trapping is enabled, then true will be returned. If an error is throw an error-trapping is NOT enabled, then
            // that error will be allowed to propagate up. If there is no error raised then the result of the IF evaluation is returned.
            // - Note: In http://blogs.msdn.com/b/ericlippert/archive/2004/08/19/error-handling-in-vbscript-part-one.aspx, Eric Lippert does sort of
            //   describe this in passing (see the note that reads "If Blah raises an error then it resumes on the Print "Hello" in either case")
            var result = true;
            HANDLEERROR(
                errorToken,
                () => { result = _valueRetriever.IF(valueEvaluator()); }
            );
            return result;
        }

        /// <summary>
        /// This is used by implementation of CINT, CSNG, CDBL and the like - it handles special cases of types such as Empty or booleans (and with error cases
        /// such as blanks string or VBScript Null) to try to extract a number. This number will be passed through the specified converter to ensure that it is
        /// translated into the desired type. If there are no applicable special cases then the value will be passed through the VAL function and then through
        /// the processor (if this fails then a TypeMismatchException will be raised).
        /// </summary>
        private T GetAsNumber<T>(object value, Func<object, T> converter) where T : struct
        {
            if (converter == null)
                throw new ArgumentNullException("nonSpecialCaseProcessor");

            value = _valueRetriever.NUM(value);
            if (value is DateTime)
                value = DateToDouble((DateTime)value);
            if (value is T)
                return (T)value;
            try
            {
                return converter(value);
            }
            catch (OverflowException e)
            {
                throw new VBScriptOverflowException((double)value, e);
            }
            catch (Exception e)
            {
                throw new TypeMismatchException(e);
            }
        }

        private bool IsDotNetNumericType(object l)
        {
            if (l == null)
                return false;
            return
                IsDotNetIntegerType(l) ||
                (l is decimal) || (l is double) || (l is float);
        }

        private bool IsDotNetIntegerType(object l)
        {
            if (l == null)
                return false;
            if (l.GetType().IsEnum)
                return true;
            return (l is byte) || (l is char) || (l is int) || (l is long) || (l is sbyte) || (l is short) || (l is uint) || (l is ulong) || (l is ushort);
        }

        /// <summary>
        /// The comparison (o == VBScriptConstants.Nothing) will return false even if o is VBScriptConstants.Nothing due to the implementation details of
        /// DispatchWrapper. This method delivers a reliable way to test for it.
        /// </summary>
        private bool IsVBScriptNothing(object o)
        {
            return ((o is DispatchWrapper) && ((DispatchWrapper)o).WrappedObject == null);
        }

        private double DateToDouble(DateTime value)
        {
            return ((DateTime)value).Subtract(VBScriptConstants.ZeroDate).TotalDays;
        }

        /// <summary>
        /// VBScript has comparisons that will return true, false or Null (meaning DBNull.Value) which is a return type that is difficult to represent
        /// without resorting to "object" (which could be anything) or an enum (which wouldn't be the end of the world). I think the best approach,
        /// though, is to return a nullable bool from methods internally and then translate this for VBScript (so null becomes DBNull.Value).
        /// The same approach works for other non-nullable types.
        /// </summary>
        private static object ToVBScriptNullable<T>(T? value) where T : struct
        {
            if (value == null)
                return DBNull.Value;
            return value.Value;
        }

        // Feed all of these straight through to the _valueRetriever we have
        public IBuildCallArgumentProviders ARGS
        {
            get { return _valueRetriever.ARGS; }
        }
        public object CALL(object target, IEnumerable<string> members, IProvideCallArguments argumentProvider)
        {
            return _valueRetriever.CALL(target, members, argumentProvider);
        }
        public void SET(object valueToSetTo, object target, string optionalMemberAccessor, IProvideCallArguments argumentProvider)
        {
            _valueRetriever.SET(valueToSetTo, target, optionalMemberAccessor, argumentProvider);
        }
        public object VAL(object o)
        {
            return _valueRetriever.VAL(o);
        }
        public object OBJ(object o)
        {
            return _valueRetriever.OBJ(o);
        }
        public object NUM(object o, params object[] numericValuesTheTypeMustBeAbleToContain)
        {
            return _valueRetriever.NUM(o, numericValuesTheTypeMustBeAbleToContain);
        }
        public object NullableNUM(object o)
        {
            return _valueRetriever.NullableNUM(o);
        }
        public object STR(object o)
        {
            return _valueRetriever.STR(o);
        }
        public bool IF(object o)
        {
            return _valueRetriever.IF(o);
        }
        public IEnumerable ENUMERABLE(object o)
        {
            return _valueRetriever.ENUMERABLE(o);
        }
    }
}
