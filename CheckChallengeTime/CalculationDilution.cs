using System;
using System.Collections.Generic;
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


    public class CalculationDilution
    {


        private readonly string FINAL_RESULT;
        private Test test;

        public CalculationDilution()
        {
            FINAL_RESULT = ChekChallengeTime.FINAL_RESULT;
        }

        internal double? CalculateResult(long resultId, INautilusServiceProvider sp)
        {
            //    var redt = new ResultEntryDetails();

            IDataLayer dal = null;
            try
            {


                dal = new DataLayer();
                dal.Connect();

                var result = dal.GetResultById(resultId);
                //Get Parent test
                test = result.Test;

                //Get all results
                var results = test.Results.ToList();

                //Get final result
                var finalResult = results.Where(r => r.Name == FINAL_RESULT).FirstOrDefault();
                if (finalResult == null)
                {
                    return null;
                }


                //cancek מביא את מ3 התוצאות הראשונות את התוצאות שאינם 
                //???Please add this condition : "or (result_status="C" and original_result is NULL)"
                //var notRejecteds = results.Where(r => (r.Status != "X" && (r.Status == "C" && r.ORIGINAL_RESULT == null)) && r.ResultId != finalResult.ResultId).ToList();
                var notRejecteds =
                    results.Where(r => (r.FormattedResult != "Not Entered" && r.ResultId != finalResult.ResultId))
                        .ToList();


                //reject אם אף תוצאה היא לא ???Please add this condition : "or (result_status="C" and original_result is NULL)"
                //reject או התוצאה השניה היא ???Please add this condition : "or (result_status="C" and original_result is NULL)"

                var result2 =
                    notRejecteds.Where(
                        r => r.ResultTemplate.Name == "תוצאה מיהול שני" || r.ResultTemplate.Name == "Dilution 2" || r.ResultTemplate.Name == "ספירה דופליקט")
                        .FirstOrDefault();
                bool b = notRejecteds.Any(r => r.RAW_NUMERIC_RESULT == null);

                if (result2 == null || notRejecteds.Count() != 2 || b)
                {
                    //???Sefi will think what happens in this case
                    Logger.WriteLogFile("לא התקיימו התנאים להכנסת הערך." + test.NAME, false);
                    //    MessageBox.Show(test.NAME + "לא התקיימו התנאים להכנסת הערך.", "Nautilus");
                    dal.Close();
                    dal = null;
                    return null;
                }
                //Sum rejected results
                double sum = notRejecteds.Sum(r => (double)r.RAW_NUMERIC_RESULT);

                //מביא מהתוצאות שאינם דחויות את מי שהפקטור שלו נמוך יותר
                double minDF = (double)notRejecteds.Min(r => r.DilutionFactor);

                //הנוסחה להכנסת ערך לתוצאה האחרונה
                var resultValue = sum / (1.1 * Math.Pow(10, Convert.ToDouble(-minDF)));
                var roundUpValue = Math.Ceiling(resultValue);
                return roundUpValue;
                //Set value to final result
                //     MessageBox.Show("Before result entry " + test.NAME);
                //      var reXml = new ResultEntryXmlHandler(sp, "Challenge - Result entry ");
                //  reXml.CreateResultEntryXml(test.TEST_ID, finalResult.ResultId, roundUpValue.ToString());
                //     var res = reXml.ProcssXmlWithOutSave();
                //     MessageBox.Show("After result entry " + test.NAME);
                //   if (!res)
                //    {
                //        CustomMessageBox.Show("שגיאה בהשמת ערך בתוצאה סופית,אנא פנה לתמיכה.");
                //        Logger.WriteLogFile(reXml.ErrorResponse, true);
                //        return null;
                //  }
                //else
                //{
                //    Logger.WriteLogFile("Calculation DilutionWF success " + test.NAME + " Result ID " + finalResult.ResultId, false);
                //    var valueByTagName = reXml.GetValueByTagName("return",     0, 0);
                //    return valueByTagName.ToString();
                //}

            }
            catch (Exception ex)
            {
                Logger.WriteLogFile(ex);
                MessageBox.Show("Error " + ex.Message + "  " + test.NAME);
                return null;

            }
            finally
            {
                if (dal != null) dal.Close();
                dal = null;
            }
        }




    }
}
