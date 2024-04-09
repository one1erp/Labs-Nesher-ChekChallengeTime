using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Objects;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Common;
using DAL;
using LSEXT;
using LSSERVICEPROVIDERLib;
using System.Runtime.InteropServices;
using One1.Controls;
using XmlService;

namespace CheckChallengeTime
{

    [ComVisible(true)]
    [ProgId("CheckChallengeTime.CheckChallengeTimeCls")]
    public class ChekChallengeTime : IWorkflowExtension
    {

        #region Fields

        private Aliquot _choosenAliquot;

        private INautilusServiceProvider sp;
        private IDataLayer dal;
        private Aliquot _currentAliquot;
        private Aliquot _parentAliquot;
        private Test _currenTest;
        private string _specifiedCategory;
        private string _specifiedStandardName;
        private List<Aliquot> _aliquotsToCheck;
        private List<string> notInStandardList;
        private List<Challenge> challenges;

        #region Constants

        private const string FINAL_RESULT_WORKFLOW = "Final Result Workflow";
        public const string FINAL_RESULT = "Final Result";
        private const string UNINOCULATED_CONTROL_TEST = "Uninoculated Control";
        private const string BLANK_TEST = " ";
        private const string RESULT_TIME0 = "Inoculation amount";
        private const string IN_SPEC = "I";
        private const string OUT_OF_SPEC = "O";
        #endregion


        #endregion


        public void Execute(ref LSExtensionParameters Parameters)
        {
            try
            {

                //   Logger.WriteLogFile("Start " + DateTime.Now, false);

                #region Initilaize

                sp = Parameters["SERVICE_PROVIDER"];
                var rs = Parameters["RECORDS"];
                string entityName = rs.Fields["NAME"].Value;
                var ntlsCon = Utils.GetNtlsCon(sp);



                //Connect to DAL
                Utils.CreateConstring(ntlsCon);
                dal = new DataLayer();
                dal.Connect();


                ////
                //var cc=dal.GetAll<Challenge>();
                //foreach (var challenge in cc)
                //{
                //    challenge.Name = challenge.Standard + " - " + challenge.Category + " - " + challenge.Microbe;
                //}
                //dal.SaveChanges();
                //dal.Close();
                //return;


                //// 


                //Get Specified Aliquot
                _choosenAliquot = dal.GetAliquotByName(entityName);


                //Get Parent Aliquot
                // _parentAliquot = dal.GetParentAliquot(choosenAliquot.AliquotId);
                _parentAliquot = _choosenAliquot.Parent.FirstOrDefault();



                //אוסף את כל האליקוטים מזמנים קודמים ויבדוק גם אותם
                _aliquotsToCheck = InitAliquotsToCheck(_choosenAliquot);
                //    Logger.WriteLogFile("_aliquotsToCheck " + _aliquotsToCheck.Count(), false);


                #endregion

                #region Validations

                #region First validations

                //מביא מהשם של הבדיקה איזה תקן היא
                string standard = _parentAliquot.TestTemplateEx.Standard;
                _specifiedStandardName = GetStandardName(standard);
                if (_specifiedStandardName == null)
                {
                    CustomMessageBox.Show("לא נמצא התקן");
                    return;
                }


                //מחזיר את הקטגוריה הספציפית
                _specifiedCategory = CheckCategory();
                if (_specifiedCategory == null)
                {
                    CustomMessageBox.Show("לא נבחרה קטגוריה");
                    return;
                }

                #endregion

                #region בדיקה אם הוזנו כל התוצאות ואם מצע הביקורת תקין

                challenges = dal.GetChallengeByStandard(_specifiedStandardName, _specifiedCategory);
                //     Logger.WriteLogFile("challenges " + challenges.Count(), false);
                List<string> emptiesResultsList = new List<string>();




                foreach (Aliquot aliquot in _aliquotsToCheck)
                {

                    bool hasChallengePerTime = false;


                    _currentAliquot = aliquot;

                    var tests = aliquot.Tests.Where(t => t.NAME != UNINOCULATED_CONTROL_TEST && t.NAME != BLANK_TEST);


                    //Add final result if not exits and recalculate result.
                    var resultValuesPerAliquot = tests.Select(test => CalculateFinalResult(test)).ToList();

                    //Create result entry xml with new values per aliquot
                    CreateResultEntry(aliquot.AliquotId, resultValuesPerAliquot);




                    foreach (Test test in tests)
                    {




                        //Get Challenge per test 
                        Challenge ch = challenges.Where(c => c.Microbe == test.NAME).FirstOrDefault();

                        string challengeTimeValue = null;
                        //מביא את הערך בזמן לפי שם האליקוט
                        if (ch != null)
                        {
                            challengeTimeValue = GetChallengeValue(aliquot.Name, ch);
                        }
                        bool hasChallengePerTest = false;
                        if (ch != null && !string.IsNullOrEmpty(challengeTimeValue))
                        {
                            hasChallengePerTime = true;
                            hasChallengePerTest = true;
                        }
                        Result finalResult = null;
                        double? resultValue = null;
                        if (test.STATUS != "X")
                        {
                            var resId = (from item in resultValuesPerAliquot where item.Test.NAME == test.NAME select item.FinalResultId).SingleOrDefault();
                            finalResult = dal.GetResultById(resId);

                            resId = 0;
                            //   MessageBox.Show(finalResult.FormattedResult + " " + finalResult.CalculatedNumericResult);
                            if (finalResult == null)
                            {
                                MessageBox.Show("Error");
                            }
                            resultValue = (from item in resultValuesPerAliquot where item.Test.NAME == test.NAME select item.FinalResultValue).SingleOrDefault();
                        }


                        if (resultValue == null)
                        //   if (finalResult != null && finalResult.CalculatedNumericResult == null)
                        {

                            if (hasChallengePerTest)//אם יש ערך בטבלת צלנג ולא הוזנה תוצאה סופית
                            {
                                emptiesResultsList.Add(aliquot.Name + " - " + test.NAME);// + "-" + "לא הוזנה או לא התקיימו התנאים לחישוב תוצאה");
                            }
                            else //אם אין ערך בטבלת צלנג וגם לא הוזנה תוצאה סופית
                            {
                                if (finalResult != null)
                                    finalResult.FormattedResult = "N/A";
                            }


                        }



                    }
                    Test uninoculatedTest =
                        aliquot.Tests.Where(x => x.NAME == UNINOCULATED_CONTROL_TEST)
                            .SingleOrDefault(); //שליפה של מצע הביקורת
                    if (uninoculatedTest != null)
                    {
                        Result uninoculatedResult = uninoculatedTest.Results.FirstOrDefault();
                        //למצע הביקורת יש תוצאה אחת בלבד
                        if (hasChallengePerTime && (uninoculatedResult.CalculatedNumericResult == null || uninoculatedResult.CalculatedNumericResult > 0))
                        {
                            string msg = "מצע הביקורת אינו סטרילי או שלא הוזנה בו תוצאה . ";
                            MessageBox.Show(msg +
                                aliquot.Name + "  -  ",
                                "Nautilus");
                            uninoculatedTest.Conclusion = OUT_OF_SPEC;
                            dal.SaveChanges();
                            dal.Close();
                            return;
                        }
                        else if (hasChallengePerTime) uninoculatedTest.Conclusion = IN_SPEC;
                        else if (!hasChallengePerTime)  //אם אין ערך בטבלת צלנג לזמן הספציצפי 
                        {
                            if (uninoculatedResult.CalculatedNumericResult == null)
                                uninoculatedTest.Results.FirstOrDefault().FormattedResult = "N/A"; ;
                            uninoculatedTest.Conclusion = IN_SPEC;
                        }
                    }




                }
                //Save updates
                // dal.SaveChanges();
                if (emptiesResultsList.Count() > 0)
                {

                    var challengeForm = new CheckChallengeCtrl(emptiesResultsList, "לא הוזנה תוצאה או לא התקיימו התנאים להכנסת תוצאה בבדיקות אלו: ",
                        true);
                    challengeForm.ShowDialog();

                }
                #endregion

                #endregion

                else //עבר את כל הוולידציות
                {
                    dal.SaveChanges();
                    dal.Close();
                    dal = new DataLayer();
                    dal.Connect();
                    _choosenAliquot = dal.GetAliquotByName(_choosenAliquot.Name);


                    _parentAliquot = _choosenAliquot.Parent.FirstOrDefault();

                    _aliquotsToCheck = InitAliquotsToCheck(_choosenAliquot);
                    notInStandardList = new List<string>();
                    foreach (Aliquot aliquot in _aliquotsToCheck)
                    {
                        _currentAliquot = aliquot;
                        var tests = aliquot.Tests.Where(t => t.NAME != UNINOCULATED_CONTROL_TEST && t.NAME != BLANK_TEST);

                        foreach (var test in tests)
                        {


                            string challengeValue = null;
                            _currenTest = test;
                            //Get challenge per test
                            var challenge =
                                challenges.FirstOrDefault(c => c.Microbe == test.NAME);

                            //Get challenge valuer per time
                            if (challenge != null)
                                challengeValue = GetChallengeValue(aliquot.Name, challenge);


                            if (challenge == null || challengeValue == null)
                            {

                                _currenTest.Conclusion = IN_SPEC;
                                // CustomMessageBox.Show("לא הוגדר Challenge " + test.NAME);
                                continue;


                            }

                            //Get final result for this test
                            var specifiedFinalResult = test.Results.SingleOrDefault(x => x.Name == FINAL_RESULT);
                            //    var specifiedFinalResult = t.Results.SingleOrDefault(x => x.Name == FINAL_RESULT);







                            if (specifiedFinalResult == null || specifiedFinalResult.CalculatedNumericResult == null)
                            {

                                CustomMessageBox.Show(" לא הוזנה או לא קיימת תוצאה סופית" + test.NAME);
                                //return;
                            }





                            //Get Challenge value 
                            //  string challengeValue = GetChallengeValue(aliquot.Name, challenge);

                            //challenge ניתוח הערך מטבלת 
                            int cv;

                            if (challengeValue.Contains("from")) //השוואה לזמן אחר באותה בדיקה
                            {

                                //Split column name
                                int indexOfColumn = (challengeValue.IndexOf("from") + 5);
                                //מביא את האינדקס של לפני שם העמודה
                                //שולף את שם העמודה ממנה ניקח את הערך
                                string columnName = challengeValue.Substring(indexOfColumn);


                                var resultFromTime = GetResultFromTime(columnName, _currenTest.NAME, FINAL_RESULT);


                                var split = challengeValue.Substring(0, indexOfColumn - 6);
                                int fv;
                                if (split == "NI")
                                {

                                    if (ClaculateChallenge((decimal)resultFromTime,
                                     (decimal)specifiedFinalResult.CalculatedNumericResult, 1))
                                    {
                                        UpdateConclusion(true);
                                    }
                                    else
                                    {
                                        UpdateConclusion(false);
                                    }
                                }


                                else
                                {
                                    CustomMessageBox.Show("ערך לא תקין " + test.NAME);
                                    break;
                                }

                            }

                            else //השוואה לאותו טסט בזמן 0 
                            {
                                if (challengeValue == "NI")
                                {
                                    var resultFromTime0 = GetResultFromTime("time 0", test.NAME, RESULT_TIME0);
                                    if (resultFromTime0 == null)
                                    {
                                        CustomMessageBox.Show("לא הוזנה תוצאה בזמן 0 " + _currentAliquot.Name + " " +
                                                              test.NAME);
                                        return;
                                    }

                                    if (ClaculateChallenge((decimal)resultFromTime0,
                                       (decimal)specifiedFinalResult.CalculatedNumericResult, 1))
                                    {
                                        UpdateConclusion(true);
                                    }
                                    else
                                    {
                                        UpdateConclusion(false);
                                    }
                                }
                                else if (int.TryParse(challengeValue, out cv)) //If is numeric value
                                {
                                    var resultFromTime0 = GetResultFromTime("time 0", test.NAME, RESULT_TIME0);
                                    if (resultFromTime0 == null)
                                    {
                                        CustomMessageBox.Show("לא הוזנה תוצאה בזמן 0 " + _currentAliquot.Name + " " +
                                                              test.NAME);

                                        return;
                                    }
                                    if (ClaculateChallenge((decimal)resultFromTime0,
                                        (decimal)specifiedFinalResult.CalculatedNumericResult,
                                        cv))
                                    {
                                        UpdateConclusion(true);
                                    }
                                    else
                                    {
                                        UpdateConclusion(false);
                                    }
                                }
                                else
                                {
                                    CustomMessageBox.Show("Challenge Value is not valid " + test.NAME);
                                }
                            }

                        }

                    }

                    if (dal.HasChanges())
                        dal.SaveChanges();

                    dal.Close();
                    dal = null;


                    //
                    if (notInStandardList.Count() > 0) //
                    {
                        var challengeForm = new CheckChallengeCtrl(notInStandardList,
                            ": הבדיקות הבאות אינם עומדות בתקן ", true);
                        challengeForm.ShowDialog();

                    }
                    else
                    {
                        CustomMessageBox.Show("כל הבדיקות עומדות בתקן.");
                    }
                }

            }
            catch (Exception e)
            {
                Logger.WriteLogFile(e);
                CustomMessageBox.Show("Error " + e.Message);
            }
            finally
            {
                if (dal != null)
                {
                    dal.Close();
                    dal = null;

                }

                Application.Exit();

            }

        }

        private void CreateResultEntry(long aliquotId, List<ResultEntryDetails> list)
        {

            var dictonary =
                list.Where(item => item != null && item.FinalResultId != null && item.FinalResultValue != null)
                    .ToDictionary(item => item.FinalResultId.ToString(), item => item.FinalResultValue.ToString());
            if (dictonary.Count > 0)
            {
                var reXml = new ResultEntryXmlHandler(sp, "Challenge - Result entry ");
                reXml.CreateResultEntryXmlWithAliquot(aliquotId, dictonary);
                var res = reXml.ProcssXml();
                if (!res)
                {
                    MessageBox.Show("Error in Result entry - אנא פנה לתמיכה");
                }
            }
        }



        private ResultEntryDetails CalculateFinalResult(Test test)
        {
            if (test.STATUS == "X") return null;
            var red = new ResultEntryDetails() { Test = test };

            var fr = test.Results.FirstOrDefault(r => r.Name == FINAL_RESULT);

            if (fr == null) // result אם לא נוסף  
            {
                //Add final result
                //    Logger.WriteLogFile("Add final result ", false);
                var loginResult = new LoginXmlHandler(sp, "Add Final Result ");
                loginResult.CreateLoginChildXml("TEST", test.TEST_ID.ToString(), "RESULT",
                    FINAL_RESULT_WORKFLOW, FindBy.Id);
                var b = loginResult.ProcssXml();
                if (!b)
                {
                    CustomMessageBox.Show(" שגיאה ביצירת תוצאה סופית,אנא פנה לתמיכה. " + test.NAME);
                    return null;
                }
                else
                {
                    var ri = loginResult.GetValueByTagName("RESULT_ID", 3);
                    var currentResultId = long.Parse(ri.ToString());
                    red.FinalResultId = currentResultId;
                    red.FinalResultValue = CalculateResult(red.FinalResultId);
                    return red;

                }
            }
            else
            {
                red.FinalResultId = fr.ResultId;
                //ReCalculate Final Result
                red.FinalResultValue = CalculateResult(fr.ResultId);
                return red;
            }

        }

        private double? CalculateResult(long resultId)
        {

            var cd = new CalculationDilution();
            return cd.CalculateResult(resultId, sp);
        }

        #region VALIDATIONS

        private string CheckCategory()
        {
            // Logger.WriteLogFile(_specifiedStandardName, false);
            switch (_specifiedStandardName)
            {
                case "USP":
                    return _parentAliquot.UspChallengeCategory;

                case "Ph .Eur":
                    return _parentAliquot.PhUerChallengeCattegory;
                default:
                    return null;

            }
        }

        private string GetStandardName(string standard)
        {
            var dic = dal.GetPhraseByName("Challenge Standard").PhraseEntriesDescriptionByName;

            //todo: להתאים לשמות מדויקים של התקני שיינתנו בהמשך
            if (standard.Contains("USP"))
            {
                return dic["2"];
            }
            if (standard.Contains("Eur"))
            {
                return dic["1"];
            }
            return null;
        }

        #endregion

        #region CALCULATIONS

        //    בדיקת תאימות לתקן תתבצע ע"י חלוקת הערך שבתוצאה בתוצאה של זמן 0 )או במקרה של סימון
        //2 בטבלה תתבצע החלוקה מול זמן 2( אם הערך שהתקבל גדול מהמספר שבטבלה אזי הוא –
        //עומד בתקן. אם הוא קטן תוכפל הספירה של זמן הייחוס בפקטור 1.25 וייבדק היחס שנית. אם הוא
        //גדול מהמספר שבטבלה אז הוא עומד בתקן אחרת הספירה הנוכחית תחולק בפקטור התיקון 1.25
        //והיחס ייבדק שוב. אם הוא גדול מהמספר שבטבלה אז הוא עומד בתקן. אחרת הוא אינו עומד
        //בתקן.
        private bool ClaculateChallenge(decimal fromTimeValue, decimal resultValue, int challengeValue)
        {
            if (resultValue == 0) return true;
            var res = fromTimeValue / resultValue;
            if (res >= challengeValue) return true;

            else
            {
                fromTimeValue = fromTimeValue * (decimal)1.45;
                res = fromTimeValue / resultValue;
                if (res >= challengeValue) return true;
                else
                {
                    resultValue = resultValue / (decimal)1.45;
                }
                res = fromTimeValue / resultValue;
                if (res >= challengeValue) return true;
                else
                {
                    return false;
                }


            }


        }

        private bool NIclaculate(decimal fromTimeValue, decimal? resultValue)
        {
            if (resultValue == 0) return true;
            return fromTimeValue / resultValue >= 1;

        }

        #endregion

        private List<Aliquot> InitAliquotsToCheck(Aliquot aliquot)
        {
            // OLD
            //string[] times = { "6h", "24h", "week 1", "week 2", "week 3", "week 4" };

            string[] times = { "6h", "24h", "48h", "7 days", "14 days", "21 days", "28 days" };
            int index;
            for (index = 0; index < times.Length; index++)
            {
                if (aliquot.Name.Contains(times[index]))//בדיקה של איזה זמן נמצאים.
                {
                    break;
                }
            }
            var aliquotsToCheck = new List<Aliquot>();
            for (int j = 0; j < index; j++)
            {
                Aliquot child = _parentAliquot.Children.Where(c => c.Name.Contains(times[j])).FirstOrDefault();
                if (child != null)
                    aliquotsToCheck.Add(child);
            }
            aliquotsToCheck.Add(aliquot);
            return aliquotsToCheck;
        }

        private decimal? GetResultFromTime(string timestr, string testName, string resultName)
        {
            //הולך לאותו חיידק בזמן שמקבל כפרמטר ובודק מולו
            Aliquot aliquotTime0 =
                _parentAliquot.Children.Where(child => child.Name.Contains(timestr)).SingleOrDefault();
            Test test = aliquotTime0.Tests.Where(t => t.NAME == testName).FirstOrDefault();
            var result = test.Results.Where(x => x.Name == resultName).FirstOrDefault();
            //TODO:לשנות את שם החיידק כפי שייקרא בסביבת אמת.
            if (result != null && result.CalculatedNumericResult != null)
            {
                return result.CalculatedNumericResult;
            }
            else
            {
                return null;
            }



        }

        private string GetChallengeValue(string aliquotName, Challenge challenge)
        {


            if (aliquotName.Contains("6h"))
                return challenge.U_6H;
            if (aliquotName.Contains("24h"))
                return challenge.U_24H;
            if (aliquotName.Contains("48h"))
                return challenge.U_2_DAYS;
            if (aliquotName.Contains("7 days") || aliquotName.Contains("week 1") || aliquotName.Contains("1 WEEK"))
                return challenge.U_1_WEEK;
            if (aliquotName.Contains("14 days") || aliquotName.Contains("week 2") || aliquotName.Contains("2 WEEKS"))
                return challenge.U_2_WEEKS;
            if (aliquotName.Contains("21 days") || aliquotName.Contains("week 3") || aliquotName.Contains("3 WEEKS"))
                return challenge.U_3_WEEKS;
            if (aliquotName.Contains("28 days") || aliquotName.Contains("week 4") || aliquotName.Contains("4 WEEKS"))
                return challenge.U_4_WEEKS;

            return null;



        }

        private void UpdateConclusion(bool b)
        {

            if (!b)
            {
                if (_currenTest != null)
                {
                    _currenTest.Conclusion = OUT_OF_SPEC;
                    notInStandardList.Add(_currentAliquot.Name + " - " + _currenTest.NAME);//שומר את הבדיקות שלא עמדו בתקן
                }
            }
            else
            {
                if (_currenTest != null)
                {
                    _currenTest.Conclusion = IN_SPEC;
                }
            }
        }


    }


}
