package main

import (
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

func wholefile(filename string) []byte {
	data, err := os.ReadFile(filename)
	if err != nil {
		panic(err)
	}
	return data
}

func main() {
	var output []byte

	output = sectionMarker("AUTO-ASSEMBLED CODE")

	output = append(output, sectionMarker("BEGIN base scheduler")...)

	output = append(output, wholefile("baseQueue.cs")...)
	output = append(output, sectionMarker("END base scheduler")...)

	dict := "public Dictionary<string,MakeSystem> SystemTypes=new Dictionary<string,MakeSystem>{\n"

	for _, section := range sections {
		output = append(output, sectionMarker("BEGIN "+section)...)
		output = append(output, wholefile(section+".cs")...)
		output = append(output, sectionMarker("END "+section)...)
		dict = dict + "\t{\"" + section + "\", Make" + section + "},\n"

	}

	dict += "};\n"
	output = append(output, sectionMarker("AUTO-GENERATED CODE")...)
	output = append(output, []byte(dict)...)
	os.WriteFile("output.cs", output, 0666)
	clipboard.Set(string(output))
}
