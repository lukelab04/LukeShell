// MIT License

// Copyright (c) 2021 Luke LaBonte

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


//THIS IS THE LINUX BUILD. THE WEBASSEMBLY VERSION IS SLIGHTLY DIFFERENT. IF YOU WANT TO RUN LUKESCRIPT LOCALLY, USE THE LINUX SHELL (linked in readme) INSTEAD.


using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.IO;

namespace shell{
    class LukeScript{    
        public static string TextBody;
        public static int Position = 0;
        public static bool Executing = true, lookingForString = false;

        public static bool breakOutOfSection = false;

        public static List<string> builtInFuncs = new List<string>(){
            "print",
            "execute"
        };

        //This holds "structure" elements (basically anything that holds code within curly braces.)
        public static List<string> Loops = new List<string>(){
            "repeat",
            "while",
            "if",
            "else"    
        };

        public static List<string> LanguageKeyWords = new List<string>(){
            "exit",
            "return"
        };
        
        //Functions are stored as variables, so they are included here.
        public static List<string> variableNames = new List<string>(){
            "num",
            "string",
            "function"
        };
        
        //Parent class to hold repeat, if, else, while, etc.
        public class Scope{
            //Local variables
            public Dictionary<string, object> localVars = new Dictionary<string, object>();

            //Return value, stored as an object to make casting easier
            public Object returnval = null;

            public Scope parentScope = null;
            //Type can be if, repeat, while, or function
            public string type = "";

            public string returnType;
        }

        //Class to hold Repeat loops. Extends Scope.
        public class Repeat : Scope{
            public int startPos, endpos, repeatAmount;
            public string content;
            public Repeat(int start, int end, int repeat, string cont){
                startPos = start;
                endpos = end;
                repeatAmount = repeat;
                content = cont;
            }
        }

        //Class to hold While loops. Extends Scope;
        public class While : Scope{
            public int startPos, endPos;
            public string content, conditional;
            public While(int start, int end, string cont, string condit){
                startPos = start;
                endPos = end;
                content = cont;
                conditional = condit;
            }
        }

        //Class to hold If statements. Extends Scope.
        public class If : Scope{
            public int startPos, endPos;
            public string content, conditional;
            public If(int start, int end, string cont, string condit){
                startPos = start;
                endPos = end;
                content = cont;
                conditional = condit;
            }
        }

        //Class to hold Functions. Extends Scope.
        public class Function : Scope{
            public string content;
            public List<string> args;

            //Constructor to assign the arguments to the local variables
            public Function(string cont, List<string> arguments){
                content = cont;
                args = arguments;

                foreach(string key in args){
                    if(key != ""){
                        localVars[key] = null;
                    }
                }
            }
        }

        public static Dictionary<string, object> variables = new Dictionary<string, object>();

        static void Main(string[] args){
            //Paths of the input and output text files
            string path = "./rundata/lukescript.txt";
            string path2 = "./rundata/lukescriptcommands.txt";
            //Clear commands file
            File.WriteAllText(path2, String.Empty);
            TextBody = File.ReadAllText(path);
            try{
                parse(TextBody);
            }
            catch(Exception e){
                Console.WriteLine(e.Message);
            }
        }

        //Gets the string that is between the nearest curly braces. 
        public  static string getsection(int pos, string input){
            //Skip to nearest open curly
            while(pos < input.Length && input[pos] != '{'){pos++;}
            if(pos >= input.Length){throw new Exception("Expected {.");}
            int curlyCount = 1;
            string section = "";
            pos++;
            //Open curlys increase curlycount, closed curlys decrease it. Once curlycount is zero, the outer closing curly has been reached.
            while(pos < input.Length && curlyCount > 0){
                if(input[pos] == '{'){curlyCount++;}
                if(input[pos] == '}'){curlyCount--;}
                if(curlyCount == 0){Position = pos; return section;}
                section += input[pos];
                pos++;
            }
            throw new Exception("Expected }.");
        }

        //Gets a single word, stops at any non-letter or '_' character.
        public static string getword(int pos, string input, bool updatePosition){
            string outword = "";
            //Collect characters and underscores 
            while(pos < input.Length && (Char.IsLetter(input[pos]) || input[pos] == '_')){
                outword += input[pos];
                pos++;
            }
            if(updatePosition) Position = pos;
            return outword;
        }

        //Eliminates the whitespace at the current position and continues until it hits a non-whitespace character.
        public static void elimWhiteSpace(string input, int pos){
            while(pos < input.Length && Char.IsWhiteSpace(input[pos])){
                pos++;
            }
            Position = pos;
            return;
        }

        //Helper function to see if a word is a built-in function
        public static bool isBuiltIn(string func){
            if(builtInFuncs.IndexOf(func) > -1){
                return true;
            }
            return false;
        }

        //Helper function to get the string contained within the nearest parenthesis. 
        public static string getParenExpr(string input, int pos){
            if(pos >= input.Length || input[pos] != '('){throw new Exception("Expected parenthesis.");}

            int totalParens = 1;
            string expr = "";
            pos++;

            //Open parenthesis increase totalparens, closed ones decrease it. Once totalparens is 0, we've reached the outer closing parenthesis. 
            while(totalParens > 0 && pos < input.Length){
                if(input[pos] == '(') totalParens++;
                else if(input[pos] == ')') totalParens--;
                expr += input[pos];
                pos++;
            }
            if(pos >= input.Length && totalParens > 0){throw new Exception("Missing parenthesis.");}
            Position = pos;

            //The first parehtnesis isn't collected, so the last parenthesis should be removed to match.
            expr = expr.Remove(expr.Length-1);
            return expr;
        }

        //Helper function to skip the position to the nearest closing parenthesis.
        public static int skipToParen(int pos, string input){
            int pcount = 0;
            int i = 0;
            for(i = pos; i < input.Length; i++){
                if(input[i] == '(' && !lookingForString){
                    pcount++;
                }
                if(input[i] == ')' && !lookingForString){
                    pcount--;
                    if(pcount == 0){
                        return i;
                    }
                }
            }
            return i;
        }

        //Helper function to get the return type of a function, which is contained within < >
        public static string getFunctionType(int pos, string input){
            int i = pos;
            //Skip to nearest <
            for(i = pos; i < input.Length; i++){
                if(input[i] == '<'){
                    break;
                }
            }
            i++;
            string typestr = "";
            //Collect entire string within <>
            for(;i < input.Length; i++){
                if(input[i] == '>'){
                    return typestr;
                }
                typestr += input[i];
            }

            if(typestr == ""){
                throw new Exception("Expected function return type.");
            }

            return typestr;
        }

        //Helper function to get the type (string, num, or function) of an input string.
        public static string gettype(string input, Scope localscope, bool skipFunctions = false){
            //Sometimes it is easier to skip functions and focus on variables or hardcoded values, hence the skipfunctions option
            
            bool foundFunction = false;
            
            for(int i = 0; i < input.Length; i++){
                //If we hit a quote, the type must be string
                if(input[i] == '"'){
                    return "string";
                }
                //If we hit a number, the type must be number
                if(char.IsNumber(input[i])){
                    return "number";
                }
                
                //If neither of those are true, the current word is a variable or function
                if(char.IsLetter(input[i])){
                    string word = getword(i, input, false);

                    //Test the localscope first to maintain scope priority 
                    if(localscope != null && localscope.localVars.ContainsKey(word)){
                        if(localscope.localVars[word].GetType() == typeof(System.String)){return "string";}
                        if(localscope.localVars[word].GetType() == typeof(System.Double)){return "number";}
                        if(localscope.localVars[word].GetType() == typeof(Function)){
                            foundFunction = true;

                            if(!skipFunctions){
                                return "function";
                            }
                            //We just have the function word, and since we're not interested in the arguments, we can skip to the closing parenthesis
                            i = skipToParen(i, input);
                        }
                    }
                    
                    //Next, test the global variables
                    else if(variables.ContainsKey(word)){
                        if(variables[word].GetType() == typeof(System.String)){return "string";}
                        if(variables[word].GetType() == typeof(System.Double)){return "number";}
                        if(variables[word].GetType() == typeof(Function)){
                            foundFunction = true;
                            if(!skipFunctions){
                                return "function";
                            }
                            i = skipToParen(i, input);
                        }
                    }
                }
            }
            //If we haven't returned by now, and we haven't been skipping functions, then there is an incorrect identifier.
            if(!foundFunction) throw new Exception("Expected identifier.");
            return "";
        }

        //Helper function to arrange a comma-separated string of arguments into a list.
        public static List<string> getargs(string input){
            string currentarg = "";
            int parenCount = 0;
            List<string> arglist = new List<string>();
            
            for(int i = 0; i < input.Length; i++){
                //Skip whitespace and document parenthesis and quotes
                if(Char.IsWhiteSpace(input[i]) && !lookingForString){continue;}
                if(input[i] == '(' && !lookingForString){parenCount++;}
                if(input[i] == ')' && !lookingForString){parenCount--;}
                if(input[i] == '"'){lookingForString =! lookingForString;}
                
                //If the input is a comma and we aren't currently collecting a function (which might have commas in the arguments) then add the current argument to the list
                if(input[i] == ',' && !lookingForString && parenCount == 0){arglist.Add(currentarg); currentarg = ""; continue;}
                currentarg += input[i];
            }
            //The last argument won't have a comma, so we need to add it here
            arglist.Add(currentarg);
            return arglist;
        }

        //Helper function to evaluate math on an input that has the type of string
        public static string evalStringMath(string input, Scope localscope){
            string finalString = "";
            
            for(int i = 0; i < input.Length; i++){
                if(input[i] == '"'){lookingForString = !lookingForString;}

                //If the current char is a letter outside of a string, it must be a variable or function
                if(Char.IsLetter(input[i]) && !lookingForString){
                    string word = getword(i, input, false);

                    if(gettype(word, localscope) == "function"){
                        //Go to parenthesis
                        while(i < input.Length && input[i] != '('){i++;}

                        //Execute the function and cast its return value to a string
                        string value = (String)executeFunction(word, i, input+ ';', localscope);

                        i = skipToParen(i, input);
                        finalString += value.ToString();
                        continue;
                    }

                    
                    //If the type of the variable is not a string, throw an error
                    if(gettype(word, localscope) != "string"){throw new Exception("Cannot convert type " + gettype(word, localscope) + " to type string.");}
                    
                    //Check the local and global scope for the variable
                    if(localscope != null && localscope.localVars.ContainsKey(word)){
                        finalString += (string)localscope.localVars[word];
                    }
                    else if(variables.ContainsKey(word)){
                        finalString += (string)variables[word];
                    }
                    else throw new Exception("Variable " + word + " is undefined." );


                    //The only operation that is supported is addition, so we can just skip to the nearest plus sign (or the end of the string)
                    while(i < input.Length && input[i] != '+'){
                        i++;
                    }
                    if(i >= input.Length){return finalString;}
                }
                if(lookingForString && input[i] != '"'){
                    finalString += input[i];
                }
            }
            return finalString;
        }

    
        //Helper function to evaluate math on an input of type number
        public static string evalNumberMath(string input, Scope localscope){
            string evalString = "";
            for(int i = 0; i < input.Length; i++){

                //If the char is a letter, it must be a function or variable
                if(Char.IsLetter(input[i])){
                    string word = getword(i, input, false);

                    if(gettype(word, localscope) == "function"){
                        //Go to parenthesis
                        while(i < input.Length && input[i] != '('){i++;}
                        
                        //Evaluate function and cast its return value to a double
                        double value = (Double)executeFunction(word, i, input+ ';', localscope);

                        i = skipToParen(i, input);
                        evalString += value.ToString();
                        continue;
                    }

                    //If the variable type isn't number, throw an exception
                    else if(gettype(word, localscope) != "number"){throw new Exception("Cannot convert type " + gettype(word, localscope) + "to type number.");}
                    
                    //Check the local and global scope for the variable
                    if(localscope != null && localscope.localVars.ContainsKey(word)){
                        evalString += localscope.localVars[word].ToString();
                    }
                    else if(variables.ContainsKey(word)){
                        evalString += variables[word].ToString();
                    }
                    else throw new Exception("Identifier " + word + " is undefined." );


                    while(i < input.Length && Char.IsLetter(input[i])){i++;}
                    if(i >= input.Length){break;}
                }
                evalString += input[i];
            }

            //C# has a built in way to parse mathematical functions, so it makes sense to just use that
            DataTable dt = new DataTable();
            object result;
            double numfinal;
            result = dt.Compute(evalString, "");
            if(result is IConvertible){
                numfinal = ((IConvertible)result).ToDouble(null);
            }
            else{
                numfinal = 0;
            }
            return numfinal.ToString();
        }

        //Helper function to execute the built-in functions. 'execute' only works in the Linux build.
        public static void executeBuiltIns(string func, int pos, string input, Scope localscope){
            string outputText = "";
            elimWhiteSpace(input, pos);
            pos = Position;
            int endind = Position;
            
            if(func == "print"){
                string expr = getParenExpr(input, pos);
                endind = Position;
                List<string> args = getargs(expr);
                
                //Loop through each argument and print its value to the terminal
                foreach(string elem in args){
                    //Get the type of the argument and evaluate it
                    if(gettype(elem, localscope) == "string"){
                        outputText += evalStringMath(elem, localscope);
                    }
                    if(gettype(elem, localscope) == "number"){
                        outputText += evalNumberMath(elem, localscope);
                    }
                    if(gettype(elem, localscope) == "function"){
                        string funcName = getword(0, elem, false);
                        int i = 0;
                        for(i = 0; i < elem.Length; i++){
                            if(elem[i] == '('){
                                break;
                            }
                        }
                        Object value = executeFunction(funcName, i, elem + ';', localscope);
                        outputText += value;
                    }
                }
                Console.Write(outputText + '\n');
            }
            //Write the command to the commands text file so that the C++ shell can read and execute it
            else if(func == "execute"){
                string path = "./rundata/lukescriptcommands.txt";
                elimWhiteSpace(input, Position);
                string exeCommand = getParenExpr(input, Position);
                File.AppendAllText(path, exeCommand.Substring(1, exeCommand.Length - 2) + "\n");
            }
            Position = endind;
        }

        //Return the string that is between the current char and the nearest non-string semicolon
        public static string getLine(int pos, string input){
            string line = "";
            pos++;
            while(pos < input.Length){
                if(input[pos] == '"'){lookingForString = !lookingForString;}
                if(input[pos] == ';' && !lookingForString){Position = pos; return line;}
                line += input[pos];
                pos++;
            }

            throw new Exception("Expected ';'");
        }


        //Jumps the position to the nearest semicolon that isn't in a string.
        public static void jumpToSemicolon(string input, int pos){
            while(pos < input.Length){
                if(input[pos] == '"'){lookingForString = !lookingForString;}
                if(input[pos] == ';' && !lookingForString){
                    Position = pos + 1;
                    return;
                }
                pos++;
            }
            throw new Exception("Expected ';'");
        }

        //Helper function to find the location of the nearest outer closing brace.
        public static int findClosingCurly(string input, int pos){
            int curlyCount = 0;
            while(pos < input.Length){
                if(input[pos] == '"'){lookingForString = !lookingForString;}
                if(input[pos] == '{' && !lookingForString){curlyCount++;}
                if(input[pos] == '}' && !lookingForString){
                    curlyCount--;
                    if(curlyCount == 0){
                        return pos;
                    }
                }
                pos++;
            }

            throw new Exception("Expected }.");
        }

        //Helper function to get the ending position of an if statment. Recursivley calls itself to account for else if and else.
        public static int getIfEndPos(string input, int pos){
            Position = pos + 1;
            elimWhiteSpace(input, Position);
            pos = Position;
            if(getword(Position, input, true) == "else"){
                return getIfEndPos(input, findClosingCurly(input, Position));
            }
            else{
                return pos;
            }
        }

        //Assigns a variable to a value. This handles functions, numbers, and strings.
        public static void assignVariable(int pos, string input, string varname, Scope localscope){
            int tempPos = Position;
            jumpToSemicolon(input, Position);
            int semiPos = Position;
            Position = tempPos;
            string evalstring = getLine(pos, input);
            
            
            if(gettype(evalstring, localscope, true) == "string"){
                //If the variable exists, set it. If not, create a new variable.
                if(localscope == null){
                    variables[varname] = evalStringMath(evalstring, localscope);
                }
                else{
                    if(localscope.localVars.ContainsKey(varname)){
                        localscope.localVars[varname] = evalStringMath(evalstring, localscope);
                    }
                    else if(variables.ContainsKey(varname)){
                        variables[varname] = evalStringMath(evalstring, localscope);
                    }
                    else{
                        localscope.localVars[varname] = evalStringMath(evalstring, localscope);
                    }
                }
                Position = semiPos - 1;
            }
            else if(gettype(evalstring, localscope, true) == "number"){
                //If the variable exists, set it. If not, create a new variable.
                if(localscope == null){
                    variables[varname] = Convert.ToDouble(evalNumberMath(evalstring, localscope));
                }
                else{
                    if(localscope.localVars.ContainsKey(varname)){
                        localscope.localVars[varname] = Convert.ToDouble(evalNumberMath(evalstring, localscope));
                    }
                    else if(variables.ContainsKey(varname)){
                        variables[varname] = Convert.ToDouble(evalNumberMath(evalstring, localscope));
                    }
                    else{
                        localscope.localVars[varname] = Convert.ToDouble(evalNumberMath(evalstring, localscope));
                    }
                }
                Position = semiPos-1;
            }

            else if(gettype(evalstring, localscope) == "function"){
                Position = 0;
                elimWhiteSpace(evalstring, Position);
                string funcName = getword(Position, evalstring, false);

                //Create a temporary function object that holds whichever function was called
                Function tempfunc = (Function)variables[funcName];
                tempfunc.type = "function";

                Object returnvar = 0;
                
                //Cast the function's return value to whatever the return type is
                if(tempfunc.returnType == "num"){
                    returnvar = Convert.ToDouble(evalNumberMath(evalstring, localscope));
                    returnvar = (Double)returnvar;
                }
                else if(tempfunc.returnType == "string"){
                    returnvar = evalStringMath(evalstring, localscope);
                    returnvar = (String)returnvar;
                }
                else{
                    throw new Exception("Unknown return type");
                }

                //If the variable exists, set it. If not, create a new variable.
                if(localscope == null){
                    variables[varname] = returnvar;
                }
                else{
                    
                    if(localscope.localVars.ContainsKey(varname)){
                        localscope.localVars[varname] = returnvar;
                    }
                    else if(variables.ContainsKey(varname)){
                        variables[varname] = returnvar;
                    }
                    else{
                        localscope.localVars[varname] = returnvar;
                    }
                }

                Position = semiPos - 1;
            }

            else{
                throw new Exception("Could not determine type of variable " + varname);
            }
        }

        //Helper function to see if a string is a logical comparator
        public static bool isLogicalComparator(string input){
            if( input == "=="   ||
                input == "!="   ||
                input == "<="   ||
                input == ">="   ||
                input == ">"    ||
                input == "<"){
                    return true;
                }
            return false;
        }

        //Helper function to get the inner conditional of an if statement (anything within parenthesis)
        public static string getInnerConditional(string conditional){
            string innerConditional = "";
            bool foundLogicalComparator = false;
            bool addingToString = false;
            int parenCount = 0;

            for(int i = 0; i < conditional.Length; i++){
                //If the previous and current characters are logical comparators, set foundlogicalcomparator to true
                if(i > 0 && isLogicalComparator(conditional[i - 1].ToString() + conditional[i].ToString())){foundLogicalComparator = true;}
                if(conditional[i] == '('){parenCount++;}
                if(conditional[i] == ')'){parenCount--;}
                if(conditional[i] == '(' && !addingToString){addingToString = true;}
                //If the current char is a closing parenthesis and we've found a logical comparator, then the string we are adding to must be an inner conditional
                if(conditional[i] == ')' && addingToString && parenCount == 0 && foundLogicalComparator){innerConditional += conditional[i]; return innerConditional;}
                //If the current char is a closing parenthesis and we haven't found a logical comparator, then whatever was inside the parenthesis wasn't an inner conditional
                if(conditional[i] == ')' && addingToString && parenCount == 0 && !foundLogicalComparator){addingToString = false; innerConditional = "";}
                if(addingToString){innerConditional += conditional[i];}

            }

            if(innerConditional != ""){return getInnerConditional(innerConditional.Substring(1, innerConditional.Length - 2));}
            return conditional;

            throw new Exception("Could not parse conditionals.");
        }

        //Helper function to compare string values based on an operator
        public static bool compareStringValues(string a, string b, string strOperator){
            switch(strOperator){
                case "==": return a == b;
                case "!=": return a != b;
            }
            throw new Exception("Cannot use operator " + strOperator + " on strings.");
        }

        //Helper function to compare number values based on an operator
        public static bool compareNumberValues(double a, double b, string strOperator){
            switch(strOperator){
                case "==": return a == b;
                case "!=": return a != b;
                case "<=": return a <= b;
                case ">=": return a >= b;
                case "<": return a < b;
                case ">": return a > b;
            }

            throw new Exception("Cannot use operator " + strOperator + " on " + a + " and " + b);
        }

        //Helper function to get one side of a conditional 
        public static string getCompareSide(ref int pos, string input){
            string side = "";
            //If the current char is any of the below characters OR the end of the string, then we must have collected one side of the string to compare
            for(int i = pos; i < input.Length; i++){
                if(input[i] == '|' || input[i] == '!' || input[i] == '&' || input[i] == '=' || input[i] == '>' || input[i] == '<'){
                    pos = i;
                    return side;
                }
                side += input[i];
            }
            return side;
        }

        //Helper function to get a logical comparator between two values
        public static string getLogicalComparator(ref int pos, string input){
            for(int i = pos; i < input.Length; i++){
                //If the current and previous characters are comparators, then we've hit a comparator 
                if(i < input.Length - 1 && isLogicalComparator(input[i].ToString() + input[i+1].ToString())){
                    string returnval = input[i].ToString() + input[i+1].ToString();
                    i += 2;
                    pos = i;
                    return returnval;
                }
                //If the current char is a logical comparator, return it (as of now only apples to '!')
                else if(isLogicalComparator(input[i].ToString())){
                    string returnval = input[i].ToString();
                    i += 1;
                    pos = i;
                    return returnval;
                }
            }
            throw new Exception("Expected comparator.");
        }

        //Helper function to get a logical operator (and or or) 
        public static string getLogicalOperator(ref int pos, string input){
            for(int i = pos; i < input.Length; i++){
                if(i < input.Length - 1 && (input[i] == '&' || input[i] == '|')){
                    string retstring = input[i].ToString() + input[i].ToString();
                    i += 2;
                    pos = i;
                    return retstring;
                }
            }
            return "";
        }

        //Helper function to evaluate a parsed conditional. Must in the format (1/0 && 1/0) or (1/0 || 1/0)
        public static bool evalParsedConditional(string input){
            while(input.Length > 1){
                //Get the first side of the conditional
                string a = input.Substring(0, 1);
                //Get the operator
                string op = input.Substring(1, 2);
                //Get the second side
                string b = input.Substring(3, 1);

                //If a and b are 1, replace the conditional currently being evaluated with a 1
                if(a == "1" && b == "1"){
                    input = input.Substring(4);
                    input = "1" + input;
                }
                
                //If either a or b is 1
                else if(a != b){
                    //If the operator is ||, replace the conditional with 1. Else, replace with 0.
                    if(op == "||"){
                        input = input.Substring(4);
                        input = "1" + input;
                    }
                    else{
                        input = input.Substring(4);
                        input = "0" + input;
                    }
                }
                //If both a and b are zero, replace the conditional with 0.
                else{
                    input = input.Substring(4);
                    input = "0" + input;
                }

            }

            //Eventually, the entire conditional will be worked down to either a 1 or a 0.
            if(input == "1"){return true;}
            return false;
        }
    
        //Helper function to evaluate a single conditional (does not handle && or ||)
        public static bool evaluateSingleConditional(string conditional, Scope localscope){
            string equivalentOutput = "";
            string rightSide = "";
            string leftSide = "";
            string logicalComparator = "";

            for(int i = 0; i < conditional.Length; i++){
                leftSide = getCompareSide(ref i, conditional);
                logicalComparator = getLogicalComparator(ref i, conditional);
                rightSide = getCompareSide(ref i, conditional);

                //If the type is string, evaluate whether it is true or false and add the bool (casted to a 1 or 0) to the end string. Do the same with numbers.
                if(gettype(leftSide, localscope) == "string"){
                    leftSide = evalStringMath(leftSide, localscope);
                    rightSide = evalStringMath(rightSide, localscope);
                    equivalentOutput += Convert.ToInt16(compareStringValues(leftSide, rightSide, logicalComparator));
                }
                else if(gettype(leftSide, localscope) == "number"){
                    double left = Convert.ToDouble(evalNumberMath(leftSide, localscope));
                    double right = Convert.ToDouble(evalNumberMath(rightSide, localscope));
                    equivalentOutput += Convert.ToInt16(compareNumberValues(left, right, logicalComparator));
                }
                logicalComparator = getLogicalOperator(ref i, conditional);
                if(logicalComparator == ""){i = conditional.Length;}
                equivalentOutput += logicalComparator;
            }
            
            //Send the paresed string to evalparsedconditional to get a 0 or a 1
            return evalParsedConditional(equivalentOutput);
        }

        //Helper function to remove any extra parenthesis from conditional statements
        public static string removeParenthesis(string input){
            for(int i = 0; i < input.Length/2; i++){
                if(input[0] == '(' && input[input.Length - 1] == ')'){
                    input = input.Substring(1, input.Length - 2);
                }
                else{
                    return input;
                }
            }
            return input;
        }

        //Function to evaluate an entire conditional statement.
        public static bool evaluateConditional(string conditional, Scope localscope){
            bool isComplete = false;
            while(!isComplete){
                //If there are no parenthesis, then there are no inner conditionals and the current statement can be evaluated
                if(!conditional.Contains(')') && !conditional.Contains('(')){return evaluateSingleConditional(conditional, localscope);}
                //Otherwise, get the inner conditional
                string innerconditional = getInnerConditional(conditional);
                string tmpinnercond = innerconditional;
                innerconditional = removeParenthesis(innerconditional);
                //Evaluate the single conditional and replace the tmpinnerconditional with its value
                bool eval = evaluateSingleConditional(innerconditional, localscope);
                if(eval){conditional = conditional.Replace(tmpinnercond, "1 == 1");}
                else{conditional = conditional.Replace(tmpinnercond, "0 == 1");}
            }

            return false;
        }


        //Helper function to create a Function object and add it to the variables dictionary 
        public static void createFunction(string funcName, string input, Scope localscope){
            string expr = getParenExpr(input, Position);
            List<string> args = getargs(expr);
            string type = getFunctionType(Position, input);
            
            string content = getsection(Position, input);
            if(!(type == "num" || type == "string" || type == "none")){throw new Exception("Cannot return function of type " + type);}
            
            Function func = new Function(content, args);
            variables[funcName] = func;
            func.returnType = type;
            func.type = "function";
        }

        //Function to execute a function given the name, position, and function arguments. Calls itself recursivley to handle inner functions.
        public static Object executeFunction(string funcName, int pos, string input, Scope localscope){
            string arguments = getParenExpr(input, pos);

            List<string> args = getargs(arguments);

            Function f = null;

            //Functions cannot be created in a local scope, so we only need to check the global variables
            if(variables.ContainsKey(funcName)){
                //Create an entirely new function and copy the values over (new is used here so that the variables don't refer to the same place in memory, which would mess with variable scope)
                Function tempfunc = (Function)variables[funcName];
                f = new Function(tempfunc.content, tempfunc.args);
                f.type = "function";
                f.localVars = new Dictionary<String, Object>(tempfunc.localVars);
                f.returnType = tempfunc.returnType;
            }else throw new Exception("Identifier " + funcName + " is undefined.");

            List<string> keys = f.localVars.Keys.ToList();

            for(int i = 0; i < keys.Count(); i++){
                //Set the local variables of the function equal to the values of the arguments supplied
                if(gettype(args[i], localscope, true) == "number"){
                    f.localVars[keys[i]] = Convert.ToDouble(evalNumberMath(args[i], localscope));
                }
                else if(gettype(args[i], localscope, true) == "string"){
                    f.localVars[keys[i]] = evalStringMath(args[i], localscope);
                }
                else if(gettype(args[i], localscope, false) == "function"){
                    string localFuncName = getword(0, args[i], false);
                    Function tempFunc = (Function)variables[localFuncName];
                    tempFunc.type = "function";
                    if(tempFunc.returnType == "num"){
                        f.localVars[keys[i]] = Convert.ToDouble(evalNumberMath(args[i], localscope));
                    }
                    if(tempFunc.returnType == "string"){
                        f.localVars[keys[i]] = evalStringMath(args[i], localscope);
                    }
                }
            }

            int tempPos = Position;
            Position = 0;
            
            //Parse the content of the function
            parse(f.content, f);
            Position = tempPos;
            
            //Once the contents have been parsed, jump the position to the end of the line
            jumpToSemicolon(input, Position);

            //If the function is supposed to return something and doesn't, throw an error
            if(f.returnval == null && f.returnType != "none"){
                throw new Exception("Null return value");
            }

            return f.returnval;
        }


        //Helper function to get the parent scope of a scope (either a function or the initial scope)
        public static Scope getParentScope(Scope s){
            if(s.parentScope == null){return s;}
            else{return getParentScope(s.parentScope);}
        }
        
        /*Bool to track whether or not a function is returning. Used because if 'return' is called in an if statement and parse simply returns, it will exit into the
            initial function and not out to whatever called the initial function.*/
        public static bool returning = false;
    
        //Function to parse and execute a LukeScript program
        public static void parse(string input, Scope currentLocalSpace = null){
            returning = false;
            while(Position < input.Length){
                
                //If the current and next characters are '/', the line is a comment and can be skipped
                if(Position < input.Length - 1 && input[Position] == '/' && input[Position+1] == '/'){
                    while(Position < input.Length && input[Position] != '\n'){Position++;}
                }

                string currentWord = getword(Position, input, true);

                if(isBuiltIn(currentWord)){
                    executeBuiltIns(currentWord, Position, input, currentLocalSpace);
                }

                if(LanguageKeyWords.Contains(currentWord)){
                    //If the current word is exit, return from parsing and set breakoutofsection to true to break out of the while loop
                    if(currentWord == "exit"){
                        breakOutOfSection = true;
                        return;
                    }
                    if(currentWord == "return" && currentLocalSpace != null){
                        //Set returning to true so that it will escape to the parent function/scope
                        returning = true;

                        if(getParentScope(currentLocalSpace).returnType == "none"){
                            return;
                        }

                        string retstring = getLine(Position, input);

                        //Get the type of the return string, evaluate it, and set the return value to that
                        if(gettype(retstring, currentLocalSpace, true) == "number"){
                            getParentScope(currentLocalSpace).returnval = Convert.ToDouble(evalNumberMath(retstring, currentLocalSpace));
                        }
                        else if(gettype(retstring, currentLocalSpace, true) == "string"){
                            getParentScope(currentLocalSpace).returnval = evalStringMath(retstring, currentLocalSpace);
                        }
                        else if(gettype(retstring, currentLocalSpace) == "function"){
                            //Elim white space
                            int i;
                            for(i = 0; i < retstring.Length; i++){
                                if(!Char.IsWhiteSpace(retstring[i])){
                                    break;
                                }
                            }
                            retstring = retstring.Substring(i);
                            string funcName = getword(0, retstring, true);

                            Function tempfunc = (Function)variables[funcName];
                            tempfunc.type = "function";

                            if(tempfunc.returnType == "num"){
                                getParentScope(currentLocalSpace).returnval = Convert.ToDouble(evalNumberMath(retstring, currentLocalSpace));
                            }
                            else if(tempfunc.returnType == "string"){
                                getParentScope(currentLocalSpace).returnval = evalStringMath(retstring, currentLocalSpace);
                            }
                            else{
                                throw new Exception("Could not parse function with return type of " + tempfunc.returnType);
                            }
                    }
                        return;
                    }
                }
                
                //If the current word is a variable declaration (num or string) then we should create a variable
                if(variableNames.Contains(currentWord)){
                    elimWhiteSpace(input, Position);
                    string variablename = getword(Position, input, true);
                    elimWhiteSpace(input, Position);
                    if(Position < input.Length && input[Position] == ';'){variables[variablename] = null;}
                    if(Position < input.Length && input[Position] == '='){assignVariable(Position, input, variablename, currentLocalSpace); jumpToSemicolon(input, Position);}
                    if(Position < input.Length && input[Position] == '('){createFunction(variablename, input, currentLocalSpace);}
                }
                
                //If the current word is if, while, or repeat
                if(Loops.Contains(currentWord)){
                    if(currentWord == "repeat"){
                        elimWhiteSpace(input, Position);
                        string loopParams = getParenExpr(input, Position);
                        elimWhiteSpace(input, Position);
                        int tempPos = Position;
                        if(gettype(loopParams, currentLocalSpace) != "number"){throw new Exception("Cannot use string as repeat amount.");}
                        string loopBody = getsection(Position, input);
                        Position = tempPos;
                        //Create a new repeat object
                        Repeat repeatloop = new Repeat(Position + 1, Position + loopBody.Length + 2, Convert.ToInt32(evalNumberMath(loopParams, currentLocalSpace)), loopBody);
                        repeatloop.type = "repeat";

                        //Give the repeat loop a copy of the global variables
                        if(currentLocalSpace != null){
                            repeatloop.localVars = currentLocalSpace.localVars;
                            repeatloop.parentScope = currentLocalSpace;
                        }
                        
                        //Iterate as many times as the repeatamount is
                        for(int i = 0; i < repeatloop.repeatAmount; i++){
                            Position = 0;
                            parse(repeatloop.content, repeatloop);
                            if(breakOutOfSection){break;}
                            repeatloop.localVars.Clear();
                        }
                        Position = repeatloop.endpos;
                        breakOutOfSection = false;
                        if(returning){return;}
                    }
                    if(currentWord == "while"){
                        elimWhiteSpace(input, Position);
                        string conditional = getParenExpr(input, Position);
                        elimWhiteSpace(input, Position);
                        int tempPos = Position;
                        string loopBody = getsection(Position, input);
                        Position = tempPos;
                        //Create a new while object
                        While whileloop = new While(Position + 1, Position + loopBody.Length + 2, loopBody, conditional);
                        whileloop.type = "while";

                        //Copy over the variables in the current scope
                        if(currentLocalSpace != null){
                            whileloop.localVars = currentLocalSpace.localVars;
                            whileloop.parentScope = currentLocalSpace;
                        }

                        while(evaluateConditional(whileloop.conditional, currentLocalSpace) && !breakOutOfSection){
                            Position = 0;
                            parse(whileloop.content, whileloop);
                            whileloop.localVars.Clear();
                        }
                        breakOutOfSection = false;
                        Position = whileloop.endPos;
                        if(returning){return;}
                    }
                    if(currentWord == "if"){
                        elimWhiteSpace(input, Position);
                        string conditional = getParenExpr(input, Position);
                        elimWhiteSpace(input, Position);
                        int tempPos = Position;
                        string ifBody = getsection(Position, input);
                        
                        //Total end is not just the end of the if statment, but the end of subsequent else if/else as well
                        int totalend = getIfEndPos(input, Position);
                        Position = tempPos;
                        //Create a new if object
                        If ifstatement = new If(Position + 1, Position + ifBody.Length + 1, ifBody, conditional);
                        ifstatement.type = "if";

                        if(currentLocalSpace != null){
                            ifstatement.localVars = currentLocalSpace.localVars;
                            ifstatement.parentScope = getParentScope(currentLocalSpace);
                        }

                        if(evaluateConditional(ifstatement.conditional, currentLocalSpace)){
                            Position = 0;
                            parse(ifstatement.content, ifstatement);
                            breakOutOfSection = false;
                            Position = totalend-1;
                        }
                        else{
                            Position = ifstatement.endPos;
                        }
                    }
                    if(returning){return;}
                }
                                                                                  
                //If the current word is a variable
                if(variables.ContainsKey(currentWord) || (currentLocalSpace != null && currentLocalSpace.localVars.ContainsKey(currentWord))){
                    //If it is a function, execute it
                    if(gettype(currentWord, currentLocalSpace, false) == "function"){
                        elimWhiteSpace(input, Position);
                        executeFunction(currentWord, Position, input, currentLocalSpace);
                    }
                    else{
                        //Set the variable equal to whatever is on the other side of the equal sign
                        elimWhiteSpace(input, Position);

                        if(input[Position] != '='){throw new Exception("Expected expression or assignment.");}
                        Position++;

                        assignVariable(Position, input, currentWord, currentLocalSpace);
                        jumpToSemicolon(input, Position);
                    }
                }
                Position++;
            }
        }
    }
}