#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TvLibrary.Log;
using TvDatabase;
using MediaPortal.Utils.Time;
using MediaPortal.Utils.Web;
using Gentle.Framework;

namespace MediaPortal.CTSportChannels
{
  /// <summary>
  /// Program data used by IParses to stored the data for each program
  /// </summary>
  public class ProgramData
  {
    #region Variables

    private string _channelId = string.Empty;
    private string _title = string.Empty;
    private string _subTitle = string.Empty;
    private string _description = string.Empty;
    private string _genre = string.Empty;
    private List<string> _actors;
    private HTTPRequest _sublink;
    private Dictionary<string, int> _months;
    private WorldDateTime _startTime;
    private WorldDateTime _endTime;
    private int _episode = 0;
    private int _season = 0;
    private bool _repeat = false;
    private bool _subtitles = false;

    #endregion

    #region Constructors/Destructors

    public ProgramData() {}

    public ProgramData(Dictionary<string, int> months) //string[] months)
    {
      _months = months;
      //if (months != null)
      //{
      //  _months = new Dictionary<string, int>();
      //  for (int i = 0; i < months.Length; i++)
      //  {
      //    _months.Add(months[i], i + 1); ;
      //  }
      //}
    }

    #endregion

    #region Properties

    // Public Properties

    public HTTPRequest SublinkRequest
    {
      get { return _sublink; }
      set { _sublink = value; }
    }

    public string ChannelId
    {
      get { return _channelId; }
      set { _channelId = value; }
    }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    /// <remarks>
    /// If the case of title being set is all UPPER case then it is converted to proper case.
    /// </remarks>
    /// <value>The title.</value>
    public string Title
    {
      get { return _title; }
      set { _title = ProperCase(NormalizeWhitespace(value)); }
    }

    public string SubTitle
    {
      get { return _subTitle; }
      set { _subTitle = ProperCase(NormalizeWhitespace(value)); }
    }

    public string Description
    {
      get { return _description; }
      set { _description = value; }
    }

    public string Genre
    {
      get { return _genre; }
      set { _genre = ProperCase(NormalizeWhitespace(value)); }
    }

    public WorldDateTime StartTime
    {
      get { return _startTime; }
      set { _startTime = value; }
    }

    public WorldDateTime EndTime
    {
      get { return _endTime; }
      set { _endTime = value; }
    }

    public int Episode
    {
      get { return _episode; }
      set { _episode = value; }
    }

    public int Season
    {
      get { return _season; }
      set { _season = value; }
    }

    #endregion

    #region Public Methods

      

    public void InitFromProgram(Program dbProgram)
    {
      _startTime = new WorldDateTime(dbProgram.StartTime);
      _endTime = new WorldDateTime(dbProgram.EndTime);
      _title = dbProgram.Title;
      _description = dbProgram.Description;
      _genre = dbProgram.Genre;
      _subTitle = dbProgram.EpisodeName;
      int.TryParse(dbProgram.EpisodeNum, out _episode);
      int.TryParse(dbProgram.SeriesNum, out _season);
    }

    //public Program ToTvProgram(string channelName)
    //{
    //  int dbIdChannel = 0;
    //  Channel dbChannel = Broker.TryRetrieveInstance<Channel>(new Key(true, "ExternalId", channelName + "-" + _channelId));
    //  if (dbChannel != null)
    //  {
    //    dbIdChannel = dbChannel.IdChannel;
    //  }

    //  return ToTvProgram(dbIdChannel);
    //}

    public Program ToTvProgram(int dbIdChannel)
    {
      WorldDateTime endTime = (_endTime == null) ? _startTime : _endTime;
      Program program = new Program(dbIdChannel, _startTime.ToLocalTime(), endTime.ToLocalTime(), _title, _description,
                                    _genre,
                                    Program.ProgramState.None, System.Data.SqlTypes.SqlDateTime.MinValue.Value,
                                    String.Empty, String.Empty,
                                    _subTitle, String.Empty, -1, String.Empty, 0);
      if (_episode > 0)
      {
        program.EpisodeNum = _episode.ToString();
      }
      if (_season > 0)
      {
        program.SeriesNum = _season.ToString();
      }
      //if (_repeat)
      //{
      //  program.Repeat = "Repeat";
      //}

      return program;
    }

    public bool HasSublink()
    {
      if (_sublink != null)
      {
        return true;
      }

      return false;
    }

    #endregion

    #region Private Methods

  
    private BasicTime GetTime(string strTime)
    {
      BasicTime time;

      try
      {
        time = new BasicTime(strTime);
      }
      catch (ArgumentOutOfRangeException)
      {
        return null;
      }
      return time;
    }

    private int GetMonth(string strMonth)
    {
      if (_months == null)
      {
        return int.Parse(strMonth);
      }
      else
      {
        return _months[strMonth];
      }
    }

    private List<string> GetActors(string strActors)
    {
      List<string> actorList = new List<string>();

      int index = 0;
      int start;
      char[] delimitors = new char[2] {',', '\n'};
      while ((start = strActors.IndexOfAny(delimitors, index)) != -1)
      {
        string actor = strActors.Substring(index, start - index);
        actorList.Add(actor.Trim(' ', '\n', '\t'));
        index = start + 1;
      }

      return actorList;
    }

    private int GetNumber(string element)
    {
      string number = string.Empty;
      int numberValue;
      bool found = false;

      for (int i = 0; i < element.Length; i++)
      {
        if (!found)
        {
          if (Char.IsDigit(element[i]))
          {
            number += element[i];
            found = true;
          }
        }
        else
        {
          if (Char.IsDigit(element[i]))
          {
            number += element[i];
          }
          else
          {
            break;
          }
        }
      }

      try
      {
        numberValue = Int32.Parse(number);
      }
      catch (Exception)
      {
        numberValue = 0;
      }

      return numberValue;
    }

    private void GetDate(string element)
    {
      if (_startTime == null)
      {
        _startTime = new WorldDateTime();
      }

      int pos = 0;
      if ((pos = element.IndexOf("/")) != -1)
      {
        _startTime.Day = Int32.Parse(element.Substring(0, pos));
      }

      int start = pos + 1;
      if ((pos = element.IndexOf("/", start)) != -1)
      {
        _startTime.Month = Int32.Parse(element.Substring(start, pos - start));
      }
    }

    /// <summary>
    /// Converts string which are all in UPPER case to proper case.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <returns>string</returns>
    private string ProperCase(string value)
    {
      // test if value is all in UPPER case
      if (value == value.ToUpper())
      {
        // convert to Title case - dependant on culture
        TextInfo text = CultureInfo.CurrentCulture.TextInfo;
        return text.ToTitleCase(value.ToLower());
      }

      return value;
    }

    /// <summary>
    /// Normalizes the whitespace in the string by replacings all
    /// sequences of whitespace characters (space, tab, new-lines)
    /// to a single space
    /// </summary>
    /// <param name="value">The string to normalize</param>
    /// <returns>The normalized string</returns>
    private string NormalizeWhitespace(string value)
    {
      Regex whitespace = new Regex(@"\s{2,}|[\s-[ ]]+", RegexOptions.Compiled);

      return whitespace.Replace(value, " ");
    }

    #endregion

   
  }
}