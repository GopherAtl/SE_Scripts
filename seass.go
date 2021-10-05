package main

import (
	"flag"
	"fmt"
	"os"
	"strings"

	clipboard "github.com/d-tsuji/clipboard"
)

var sections = []string{"Pinger", "Airlock", "Arm"}

func sectionMarker(label string) []byte {
	numStars := 70 - 4 - len(label)
	if numStars < 2 {
		numStars = 2
	}
	return []byte("\n/" + strings.Repeat("*", numStars/2) + " " + label + " " + strings.Repeat("*", (numStars+1)/2) + "/\n\n")
}

func wholefile(filename string, stripComments bool, stripBlanks bool) []byte {
	fmt.Print("file " + filename + "\n")
	data, err := os.ReadFile(filename)
	if err != nil {
		panic(err)
	}
	//scan past required header cruft to 2nd {
	str := string(data)
	index := strings.Index(str, "{")
	str = str[index+1:]
	index = strings.Index(str, "{")
	str = str[index+1:]
	index = strings.LastIndex(str, "}")
	str = str[:index]
	index = strings.LastIndex(str, "}")
	str = str[:index]
	str = strings.TrimSpace(str)

	// if you've done something silly, like put a block comment ending in a
	// line comment, this will break. That's on you, says me. By which I mean
	// me, since this is intended for personal use only.

	/* example
	// this will break it*/

	if stripComments {
		//line comments first
		for {
			index = strings.Index(str, "//")
			if index == -1 {
				break
			}
			tempStr := str[index+2:]
			index2 := strings.Index(tempStr, "\n")
			str = str[:index]
			if index2 != -1 {
				str += tempStr[index2:]
			}
		}
		//now block comments
		for {
			index = strings.Index(str, "/*")
			if index == -1 {
				break
			}
			tempStr := str[index+2:]
			index2 := strings.Index(tempStr, "*/")
			str = str[:index] + tempStr[index2+2:]
		}
	}
	if stripBlanks {
		fmt.Print("stripping blanks\n")
		index = 0
		for index < len(str) {
			index2 := strings.Index(str[index:], "\n")
			if index2 == -1 {
				break
			}
			index += index2
			if index == len(str) {
				str = str[:index]
				break
			}
			str = strings.TrimSpace(str[:index]) + str[index:]
		}
	}
	return []byte(str)
}

func main() {

	headers := flag.Bool("headers", false, "insert section heading comments")
	stripComments := flag.Bool("nocomment", false, "strip all comments from output")
	stripBlanks := flag.Bool("noblank", false, "strip all blank lines from output")
	outputFilename := flag.String("output", "", "the output file name, does not write to a file by default")
	flag.Parse()

	if len(flag.Args()) > 0 {
		sections = flag.Args()
	}
	var output []byte

	if *headers {
		output = append(output, sectionMarker("BEGIN base scheduler")...)
	}

	output = append(output, wholefile("baseQueue.cs", *stripComments, *stripBlanks)...)
	output = append(output, []byte("\n")...)
	dict := "public Dictionary<string,SystemMaker> SystemTypes=new Dictionary<string,SystemMaker>{\n"

	for _, section := range sections {
		if *headers {
			output = append(output, sectionMarker("BEGIN "+section)...)
		}
		output = append(output, wholefile(section+".cs", *stripComments, *stripBlanks)...)
		output = append(output, []byte("\n")...)
		section = strings.ToUpper(section[:1]) + strings.ToLower(section[1:])
		dict = dict + "\t{\"" + section + "\", Make" + section + "},\n"

	}

	dict += "};\n"
	if *headers {
		output = append(output, sectionMarker("AUTO-GENERATED CODE")...)
	}
	output = append(output, []byte(dict)...)
	/**/
	if *outputFilename != "" {
		os.WriteFile(*outputFilename, output, 0666)
	}
	clipboard.Set(string(output))
}
