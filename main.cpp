#include <sys/wait.h>
#include <unistd.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <iostream>
#include <vector>
#include <fstream>
#include <sstream>
#include <readline/readline.h>
#include <readline/history.h>


using namespace std;

char** args;
string currentDirectory = get_current_dir_name();
char username[32];

vector<string> builtins = {
    "cd",
    "clear",
    "lukescript"
};

bool isBuiltIn(string command){
    for(auto elem : builtins){
        if(elem == command){return true;}
    }
    return false;
}

void shlLaunch(char** args){
    pid_t pid;
    int status;

    if((pid = fork()) < 0){exit(1);}
    else if(pid == 0){
        if(execvp(args[0], args) < 0){
            perror("shl");
            exit(1);
        }
    }
    else{
        if((pid = wait(&status)) == -1){perror("shl");}
    }
}


void tokenize(string input){
    vector<string> words;
    string currWord = "";
    for(auto letter : input){
        if(letter == ' '){words.push_back(currWord); currWord = "";}
        //This stops at 15 because splitting the arguments into multiple sections is easier than reallocating the char* to whatever size the argument is
        else if(currWord.length() == 15){words.push_back(currWord); currWord = string(1, letter);}
        else if(letter == '|'){cout << "Hit pipe " << endl;}
        else{currWord += letter;}
    }
    words.push_back(currWord);

    args = new char*[words.size() + 1];
    
    for(int i = 0; i < words.size(); i++){
        args[i] = &words[i][0];
    }
    args[words.size()] = NULL;

}

void executeBuiltIns(string builtin, string input = ""){
    if(builtin == "cd"){
        if(args[1] == NULL){
            fprintf(stderr, "shl: Expected path. \n");
        }
        else{
            if(chdir(args[1]) != 0){
                perror("shl");
            }
            currentDirectory = get_current_dir_name();
        }
    }
    if(builtin == "clear"){
        system("clear");
    }
    if(builtin == "lukescript"){

        if(strcmp(args[1], "-f") == 0){
            
            string pathstr = "";
            int i = 2;
            while(args[i] != NULL){
                pathstr += args[i];
                i++;
            }

            ifstream file(pathstr, std::ios::in);
            if(!file.good()){perror("shl"); return;}
            stringstream ss;
            ss << file.rdbuf();
            input = "lukescript " + ss.str(); 
            file.close();
        }


        ofstream file("./rundata/lukescript.txt", std::ios::out | std::ios::trunc);
        file << input.substr(10);
        file.close();

        string shellpath = "./bin/Debug/net5.0/shell";

        char* tempArgs[] = {&shellpath[0], NULL};

        shlLaunch(tempArgs);

        ifstream file2("./rundata/lukescriptcommands.txt");
        string commands;
        stringstream SS;
        SS << file2.rdbuf();
        commands = SS.str();
        commands = commands.substr(0, commands.length() - 1);

        if(commands.size() > 0){
            tokenize(commands);
            shlLaunch(args);
        }
    }


}



int main(){

    cuserid(username);
    const char* userInput;
    userInput = readline(("[\001\e[1;36m\002" + string(username) + "\001\e[1;0m\002] [\001\e[1;32m\002" + currentDirectory + "\001\e[1;0m\002]>").c_str());
    while(strcmp(userInput, "exit") != 0){
        if(*userInput) add_history(userInput);

        tokenize(userInput);

        if(isBuiltIn(args[0])){
            executeBuiltIns(args[0], userInput);
        }
        else{
            shlLaunch(args);
        }

        userInput = readline(("[\001\e[1;36m\002" + string(username) + "\001\e[1;0m\002] [\001\e[1;32m\002" + currentDirectory + "\001\e[1;0m\002]>").c_str());
        delete []args;
    }

    return 0;
}