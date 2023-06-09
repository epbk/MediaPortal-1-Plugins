﻿using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Tools;
using System.Xml;
using System.Text.RegularExpressions;
using System.Threading;

namespace MediaPortal.Pbk.Cornerstone.ScraperEngine.Nodes {
    [ScraperNode("if", LoadNameAttribute=false)]
    public class IfNode : ScraperNode {
        public string Test {
            get { return test; }
        } protected string test;

        public IfNode(XmlNode xmlNode, ScriptableScraper context)
            : base(xmlNode, context)
        {

            // Load attributes
            foreach (XmlAttribute attr in xmlNode.Attributes) {
                switch (attr.Name) {
                    case "test":
                        test = attr.Value;
                        break;
                }
            }

            // Validate TEST attribute
            if (test == null) {
                logger.Error("Missing TEST attribute on: " + xmlNode.OuterXml);
                loadSuccess = false;
                return;
            }
        }

        public override void Execute(Dictionary<string, string> variables) {
            if (Context.DebugMode) logger.Debug("executing if: " + xmlNode.Attributes.GetNamedItem("test").InnerText);

            // try to split the test on the operator, quit if we fail
            Regex splitter = new Regex("\\s*(.*?)\\s*(>=|<=|!=|=|<|>)\\s*(.*)$");
            Match match = splitter.Match(test);
            if (match.Groups.Count != 4) {
                logger.Error("Error parsing test for: " + test);
                return;
            }

            string left  = match.Groups[1].Value;
            string op    = match.Groups[2].Value;
            string right = match.Groups[3].Value;
            
            left = parseString(variables, left);
            right = parseString(variables, right);

            if (Context.DebugMode) logger.Debug("if node left value: " + (left.Length < 500 ? left : "[not logged due to size]") + "     right value: " + (right.Length < 500 ? right : "[not logged due to size]"));

            float leftNum = 0;
            float rightNum = 0;
            
            bool numeric = float.TryParse(left, out leftNum);
            numeric = numeric && float.TryParse(right, out rightNum);

            // try to process our test
            bool testPassed;
            if (op == ">=") {
                if (numeric) testPassed = leftNum >= rightNum;
                else testPassed = left.CompareTo(right) >= 0;
            } 
            else if (op == "<=") {
                if (numeric) testPassed = leftNum <= rightNum;
                else testPassed = left.CompareTo(right) <= 0;
            } 
            else if (op == "!=") {
                if (numeric) testPassed = leftNum != rightNum;
                else testPassed = left.CompareTo(right) != 0;
            } 
            else if (op == "=") {
                if (numeric) testPassed = leftNum == rightNum;
                else testPassed = left.CompareTo(right) == 0;
            } 
            else if (op == "<") {
                if (numeric) testPassed = leftNum < rightNum;
                else testPassed = left.CompareTo(right) < 0;
            } 
            else if (op == ">") {
                if (numeric) testPassed = leftNum > rightNum;
                else testPassed = left.CompareTo(right) > 0;
            }
            else {
                logger.Error("Unrecognized operator: " + op);
                return;
            }

            // if the test passed exxecute the child nodes
            if (testPassed)
                executeChildren(variables);
        }
    }
}
