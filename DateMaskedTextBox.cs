using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Digitsrl.Controls.WinForm.DateMaskedTextBox
{
    public enum DateMasked_Style { European, USA, Asian }
    public enum DateMasked_Status
    {
        /// <summary>This value is set at init</summary>
        Default = 0,
        /// <summary>With this value the date component is still not calculated</summary>
        Unknown_StillToBeValidated,
        /// <summary>A valid date is present</summary>
        Valid = 1,
        /// <summary>A valid empty date is present</summary>
        Valid_Empty,
        /// <summary>The part of the day in the date is missing</summary>
        Incomplete_MissingDay,
        /// <summary>The part of the month in the date is missing</summary>
        Incomplete_MissingMonth,
        /// <summary>The part of the year in the date is missing</summary>
        Incomplete_MissingYear,
        /// <summary>The date is invalid</summary>
        InvalidDate,
        /// <summary>An empty value is present and i expect one</summary>
        Invalid_Empty,
        /// <summary>The day is invalid, it's over 31</summary>
        Invalid_Day,
        /// <summary>The month is invalid, it's over 12</summary>
        Invalid_Month,
        /// <summary>The year is invalid</summary>
        Invalid_Year,
        /// <summary>The date is valid but is in the future</summary>
        FutureDate,
        /// <summary>The date is valid but the person is under the minimum age</summary>
        UnderAge,
        /// <summary>The date is valid but the person is too old</summary>
        TooOld,
    }
    /// <summary>This control is use to insert a date just by keyboard</summary>
    public class DateMaskedTextBox : MaskedTextBox
    {
        #region Constants
        /// <summary>This MUSK PRETEND Italian FORMAT! that is dd/MM/yyyy </summary>
        private const string mask_european = "00/00/0000";
        private const string mask_Asian = "0000/00/00";
        private const string emptyText = "  /  /";
        private const int mask_completed_lenght = 10;
        private const int mask_2DigitYear_lenght = 8;

        public const int MinimumAge_Default = 18;
        public const int MaxAge_Default = 93;
        private const int european_format_dayIndex = 0;//If you use US format MM/dd/yyyy, this should be 3
        private const int european_format_monthIndex = 3;//If you use US format MM/dd/yyyy, this should be 0

        private const int usa_format_dayIndex = 3;//If you use US format MM/dd/yyyy, this should be 3
        private const int usa_format_monthIndex = 0;//If you use US format MM/dd/yyyy, this should be 0

        public static Color OkColor = Color.FromArgb(160, 213, 73);
        public static Color ErrorColor = Color.FromArgb(255, 128, 128);
        #endregion
        #region Internal Variables
        /// <summary>This is the internal value that after the OnLeave will produce a real value</summary>
        private DateTime lastDecodedValue;
        private int _minimumAge;
        /// <summary>Any day after this internal value will be considered invalid</summary>
        private DateTime _minimunAgeDate;
        private int _maxAge;
        /// <summary>Any day before this internal value will be considered invalid</summary>
        private DateTime _maxAgeDate;
        #endregion
        #region Events
        /// <summary>Event that will be raised when the status of the date will change</summary>
        public event EventHandler<DateMasked_Status> StatusChanged;
        #endregion
        #region Proprieties
        /// <summary>The status of this TextBox, if it contain a valid date</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DateMasked_Status Status { get; private set; }
        /// <summary>The style of the date, if it's European, USA or Asian</summary>
        /// <remarks>Is set at runtime in the Constructor</remarks>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DateMasked_Style Style { get; private set; }
        /// <summary>If a valid date is inserted will return the DateTime format of what's inserted</summary>
        public DateTime ValueAsDateTime
        {
            get
            {
                if (Status != DateMasked_Status.Unknown_StillToBeValidated)
                    return lastDecodedValue;
                else
                    return DateTime.MinValue;
            }
            set
            {
                //When i set a ZERO value make it empty
                if (value.Ticks == 0L)
                {
                    this.Clear();
                    return;
                }
                Text = Style switch
                {
                    DateMasked_Style.USA => value.ToString("MM/dd/yyyy"),
                    DateMasked_Style.Asian => value.ToString("yyyy/MM/dd"),
                    _ => value.ToString("dd/MM/yyyy"),
                };
                lastDecodedValue = value;
                //Now call the validator
                VerifyContrainsOnDate();
            }
        }

        
        /// <summary>How many year must have been passed to be a valid date</summary>
        /// <remarks>Used when is a birthday and you need to set a certain age</remarks>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int MinimumAge
        {
            get { return _minimumAge; }
            set
            {
                _minimumAge = value;
                //if the minimum is below 0 the max bithday is for ever in the future
                if (_minimumAge > 1)
                    _minimunAgeDate = DateTime.Now.AddYears(-1 * value);
                else
                    _minimunAgeDate = DateTime.MaxValue;
            }
        }

        /// <summary>How in the past this date can go</summary>
        /// <remarks>Used when you need to set an expiration for a document for example</remarks>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int MaxAge
        {
            get { return _maxAge; }
            set
            {
                _maxAge = value;
                //If the max age is 0 or less, the first valid date is for ever in the past
                if (_maxAge > 1)
                    _maxAgeDate = DateTime.Now.AddYears(-1 * value);
                else
                    _maxAgeDate = DateTime.MinValue;
            }
        }
        /// <summary>If false and date are after today will return an Error</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool AllowFutureDate { get; set; }
        /// <summary>If true will report an InvalidDate on an empty string</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool AllowEmptyDate { get; set; }
        #endregion
        #region Init
        public DateMaskedTextBox()
        {
            Status = DateMasked_Status.Default;
            //I will set the mask based on the current culture
            switch (CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.ToLower())
            {
                //case "dd/mm/yyyy":
                //    Mask = mask_european;
                //    Style = DateMasked_Style.European;
                //    break;
                case "mm/dd/yyyy":
                    Mask = mask_european;
                    Style = DateMasked_Style.USA;
                    break;
                case "yyyy/mm/dd":
                    Mask = mask_Asian;
                    Style = DateMasked_Style.Asian;
                    break;
                default://Default is European
                    Mask = mask_european;
                    Style = DateMasked_Style.European;
                    break;
            }
            
            MinimumAge = MinimumAge_Default;
            MaxAge = MaxAge_Default;
            KeyPress += on_KeyPress;
            //When i press a back and delete button i will reset the status
            KeyDown += (s, e) => 
            { 
                if ((e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) && Status != DateMasked_Status.Unknown_StillToBeValidated)
                {
                    Status = DateMasked_Status.Unknown_StillToBeValidated;
                    BackColor = Color.Empty;
                    StatusChanged?.Invoke(this, Status);
                }
            };
            Leave += on_Leave_ValidateTheDate;
        }
        #endregion
        #region Events
        private void on_KeyPress(object sender, KeyPressEventArgs e)
        {
            //What ever i write will clean all precious calculated values
            if (Status != DateMasked_Status.Unknown_StillToBeValidated | e.KeyChar == (char)Keys.Back)
            {
                Status = DateMasked_Status.Unknown_StillToBeValidated;
                BackColor = Color.Empty;
                StatusChanged?.Invoke(this, Status);
            }
                
            //I want to intercept ONLY the separator character
            //separator are: \ / . -
            if (e.KeyChar != '\\' && e.KeyChar != '/' && e.KeyChar != '.' && e.KeyChar != '-')
                return;
            
            //Mask is always 00/00/0000
            //And accept an input as 00/00/00
            //use switch statement to handle different cases
            switch (SelectionStart)
            {
                case 1:
                    //if i press it after just 1 number (the day) it will add a 0 in front of it
                    //and the SelectionStart will be 3
                    if (Style != DateMasked_Style.Asian)
                    {
                        Text = Text.Insert(0, "0");
                        SelectionStart = 3;
                    }
                    break;
                case 2:
                    if (Style == DateMasked_Style.Asian)
                    {
                        Text = Text.Insert(2, "0");
                        SelectionStart = 3;
                    }
                    break;
                case 4:
                    //if i press it after just 1 number (the month) it will add a 0 in front of it
                    //and the SelectionStart will be 6
                    Text = Text.Insert(3, "0");
                    SelectionStart = 6;
                    break;
                case 5:
                    if (Style == DateMasked_Style.Asian)
                    {
                        Text = Text.Insert(2, "0");
                        SelectionStart = 3;
                    }
                    break;
            }
        }

        private void on_Leave_ValidateTheDate(object sender, EventArgs e)
        {
            //If somehow i'm too long do not validate
            if (Text.Length <= mask_completed_lenght)
            {
                //If i'm empty i will return an error if EmptyDateIsValid is false
                if (Text == emptyText)
                {
                    if (AllowEmptyDate)
                    {
                        Status = DateMasked_Status.Valid_Empty;
                        BackColor = Color.Empty;
                    }
                    else
                    {
                        Status = DateMasked_Status.Invalid_Empty;
                        BackColor = ErrorColor;
                    }
                    StatusChanged?.Invoke(this, Status);
                    return;
                }
                //If i'm not empty i will validate the date
                //Get the day and month
                string day, month,year;
                switch (Style)
                {
                    case DateMasked_Style.USA:
                        day = Text.Substring(usa_format_dayIndex, 2);
                        month = Text.Substring(usa_format_monthIndex, 2);
                        break;
                    case DateMasked_Style.Asian:
                        year = Text.Substring(0, 4);
                        if (year.Length != 4)
                        {
                            StatusChanged?.Invoke(this, DateMasked_Status.Incomplete_MissingYear);
                            Status = DateMasked_Status.Incomplete_MissingYear;
                            return;
                        }
                        month = Text.Substring(5, 2);
                        //int dayIndex = "0000/00/00".Length;
                        day = (Text.Length > 8) ? Text.Substring(8, Text.Length - 8) : string.Empty;
                        break;
                    default://is DateMasked_Style.European:
                        day = Text.Substring(european_format_dayIndex, 2);
                        month = Text.Substring(european_format_monthIndex, 2);
                        break;
                }

                if (string.IsNullOrWhiteSpace(day))
                { 
                    StatusChanged?.Invoke(this, DateMasked_Status.Incomplete_MissingDay);
                    Status = DateMasked_Status.Incomplete_MissingDay; 
                    return;
                }
                if (int.Parse(day) > 31)
                {
                    StatusChanged?.Invoke(this, DateMasked_Status.Invalid_Day);
                    Status = DateMasked_Status.Invalid_Day;
                    BackColor = ErrorColor;
                    return;
                }
                if (string.IsNullOrWhiteSpace(month))
                {
                    StatusChanged?.Invoke(this, DateMasked_Status.Incomplete_MissingMonth);
                    Status = DateMasked_Status.Incomplete_MissingMonth;
                    return;
                }
                if (int.Parse(month) > 12)
                {
                    StatusChanged?.Invoke(this, DateMasked_Status.Invalid_Month);
                    Status = DateMasked_Status.Invalid_Month;
                    BackColor = ErrorColor;
                    return;
                }

                //I validate positivily ONLY when the lenght is 10, so 00/00/0000
                //if the length is 8, so 00/00/00, i will add 19 or 20 in front of the year
                if (Style != DateMasked_Style.Asian)
                {
                    if (Text.Length == mask_2DigitYear_lenght)
                    {
                        //get the last 2 characters of the string
                        year = Text.Substring(6, 2);
                        //convert the string to an integer
                        int age = DateTime.Now.Year - int.Parse(year) + 2000;
                        if (age < _minimumAge)
                            Text = Text.Insert(6, "20");
                        else
                            Text = Text.Insert(6, "19");
                    }
                    if (Text.Length != mask_completed_lenght)
                    {
                        StatusChanged?.Invoke(this, DateMasked_Status.Incomplete_MissingYear);
                        Status = DateMasked_Status.Incomplete_MissingYear;
                        BackColor = ErrorColor;
                        return;
                    }
                }
            }
            if (DateTime.TryParse(Text, out lastDecodedValue) == false)
            {
                StatusChanged?.Invoke(this, DateMasked_Status.InvalidDate);
                Status = DateMasked_Status.InvalidDate;
                BackColor = ErrorColor;
                return;
            }
            VerifyContrainsOnDate();
        }

        /// <summary>Verify if the internal Date respect the limit of minimum/max age and future date</summary>
        /// <returns></returns>
        /// <remarks>Created to be used when you set a date from outside</remarks>
        private bool VerifyContrainsOnDate()
        {
            //calculate how many years of difference from DateTime.Now and outDate
            if (AllowFutureDate == false && lastDecodedValue > DateTime.Now)
            {
                StatusChanged?.Invoke(this, DateMasked_Status.FutureDate);
                Status = DateMasked_Status.FutureDate;
                BackColor = ErrorColor;
                return false;
            }
            //The minimumAgeDate is the date that is the minimum date acceptable
            if (_minimumAge > 1 && lastDecodedValue > _minimunAgeDate)
            {
                StatusChanged?.Invoke(this, DateMasked_Status.UnderAge);
                Status = DateMasked_Status.UnderAge;
                BackColor = ErrorColor;
                return false;
            }
            //The maxAgeDate is the oldest date that is acceptable
            if (lastDecodedValue < _maxAgeDate)
            {
                StatusChanged?.Invoke(this, DateMasked_Status.TooOld);
                Status = DateMasked_Status.TooOld;
                BackColor = ErrorColor;
                return false;
            }
            BackColor = OkColor;
            StatusChanged?.Invoke(this, DateMasked_Status.Valid);
            Status = DateMasked_Status.Valid;
            return true;
        }
        #endregion
        #region Simple GUI
        //intercept the Clear method
        public void Clear()
        {
            Text = emptyText;
            Status = DateMasked_Status.Unknown_StillToBeValidated;
            BackColor = Color.Empty;
        }
        #endregion
    }
}
