﻿module AssayTests

open Argu
open Expecto
open TestingUtils
open ISADotNet
open ArcCommander
open ArgumentProcessing
open ArcCommander.CLIArguments
open ArcCommander.APIs


let standardISAArgs = 
    Map.ofList 
        [
            "investigationfilename","isa.investigation.xlsx";
            "studiesfilename","isa.study.xlsx";
            "assayfilename","isa.assay.xlsx"
        ]

let processCommand (arcConfiguration:ArcConfiguration) commandF (r : 'T list when 'T :> IArgParserTemplate) =

    let g = groupArguments r
    Prompt.createArgumentQueryIfNecessary "" "" g 
    |> snd
    |> commandF arcConfiguration

let setupArc (arcConfiguration:ArcConfiguration) =
    let investigationArgs = [InvestigationCreateArgs.Identifier "TestInvestigation"]
    let arcArgs : ArcInitArgs list =  [] 

    processCommand arcConfiguration ArcAPI.init             arcArgs
    processCommand arcConfiguration InvestigationAPI.create investigationArgs


[<Tests>]
let testAssayTestFunction = 

    let testDirectory = __SOURCE_DIRECTORY__ + @"/TestFiles/"
    let investigationFileName = "isa.investigation.xlsx"
    let investigationFilePath = System.IO.Path.Combine(testDirectory,investigationFileName)

    testList "AssayTestFunctionTests" [
        testCase "MatchesAssayValues" (fun () -> 
            let testAssay = ISADotNet.XLSX.Assays.fromString
                                "protein expression profiling" "http://purl.obolibrary.org/obo/OBI_0000615" "OBI"
                                "mass spectrometry" "" "OBI" "iTRAQ" "a_proteome.txt" []
            let investigation = ISADotNet.XLSX.Investigation.fromFile investigationFilePath
            // Positive control
            Expect.equal investigation.Studies.Value.Head.Assays.Value.Head testAssay "The assay in the file should match the one created per hand but did not"
            // Negative control
            Expect.notEqual investigation.Studies.Value.Head.Assays.Value.[1] testAssay "The assay in the file did not match the one created per hand and still returned true"
        )
        testCase "ListsCorrectAssaysInCorrectStudies" (fun () -> 
            let testAssays = [
                "BII-S-1",["a_proteome.txt";"a_metabolome.txt";"a_transcriptome.txt"]
                "BII-S-2",["a_microarray.txt"]
            ]
            let investigation = ISADotNet.XLSX.Investigation.fromFile investigationFilePath

            testAssays
            |> List.iter (fun (studyIdentifier,assayIdentifiers) ->
                match ISADotNet.API.Study.tryGetByIdentifier studyIdentifier investigation.Studies.Value with
                | Some study ->
                    let actualAssayIdentifiers = study.Assays.Value |> List.map (fun a -> a.FileName.Value)
                    Expect.sequenceEqual actualAssayIdentifiers assayIdentifiers "Assay Filenames did not match the expected ones"
                | None -> failwith "Study %s could not be taken from the ivnestigation even though it should be there"                    
            )
        )

    ]
    |> testSequenced

[<Tests>]
let testAssayRegister = 

    let testDirectory = __SOURCE_DIRECTORY__ + @"/TestResult/assayRegisterTest"

    let configuration = 
        ArcConfiguration.create 
            (Map.ofList ["workdir",testDirectory;"verbosity","2"]) 
            (standardISAArgs)
            Map.empty Map.empty Map.empty Map.empty

    testList "AssayRegisterTests" [

        testCase "AddToExistingStudy" (fun () -> 

            let assayIdentifier = "TestAssay"
            let assayFileName = IsaModelConfiguration.getAssayFileName assayIdentifier configuration
            let measurementType = "TestMeasurementType"
            let studyIdentifier = "TestStudy"
            
            let studyArgs : StudyRegisterArgs list = [Identifier studyIdentifier]
            let assayArgs : AssayRegisterArgs list = [AssayRegisterArgs.StudyIdentifier studyIdentifier;AssayRegisterArgs.AssayIdentifier assayIdentifier;MeasurementType measurementType]
            let testAssay = ISADotNet.XLSX.Assays.fromString measurementType "" "" "" "" "" "" assayFileName []
            
            setupArc configuration
            processCommand configuration StudyAPI.register studyArgs
            processCommand configuration AssayAPI.register assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            match API.Study.tryGetByIdentifier studyIdentifier investigation.Studies.Value with
            | Some study ->
                let assayOption = API.Assay.tryGetByFileName assayFileName study.Assays.Value
                Expect.isSome assayOption "Assay was not added to study"
                Expect.equal assayOption.Value testAssay "Assay is missing values"
            | None ->
                failwith "Study was not added to the investigation"
        )
        testCase "DoNothingIfAssayExisting" (fun () -> 
            let assayIdentifier = "TestAssay"
            let assayFileName = IsaModelConfiguration.getAssayFileName assayIdentifier configuration
            let measurementType = "TestMeasurementType"
            let studyIdentifier = "TestStudy"
            
            let assayArgs : AssayRegisterArgs list = [AssayRegisterArgs.StudyIdentifier studyIdentifier;AssayRegisterArgs.AssayIdentifier assayIdentifier]
            let testAssay = ISADotNet.XLSX.Assays.fromString measurementType "" "" "" "" "" "" assayFileName []
            
            processCommand configuration AssayAPI.register assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            match API.Study.tryGetByIdentifier studyIdentifier investigation.Studies.Value with
            | Some study ->
                let assayOption = API.Assay.tryGetByFileName assayFileName study.Assays.Value
                Expect.isSome assayOption "Assay is no longer part of study"
                Expect.equal assayOption.Value testAssay "Assay values were removed"
                Expect.equal study.Assays.Value.Length 1 "Assay was added even though it should't have been as an assay with the same identifier was already present"
            | None ->
                failwith "Study was removed from the investigation"
        )
        testCase "AddSecondAssayToExistingStudy" (fun () -> 

            let oldAssayIdentifier = "TestAssay"
            let oldAssayFileName = IsaModelConfiguration.getAssayFileName oldAssayIdentifier configuration
            let oldmeasurementType = "TestMeasurementType"
            let studyIdentifier = "TestStudy"
            
            let newAssayIdentifier = "SecondTestAssay"
            let newAssayFileName = IsaModelConfiguration.getAssayFileName newAssayIdentifier configuration
            let newmeasurementType = "OtherMeasurementType"

            let assayArgs = [AssayRegisterArgs.StudyIdentifier studyIdentifier;AssayRegisterArgs.AssayIdentifier newAssayIdentifier;MeasurementType newmeasurementType]
            let testAssay = ISADotNet.XLSX.Assays.fromString newmeasurementType "" "" "" "" "" "" newAssayFileName []
            
            processCommand configuration AssayAPI.register assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            match API.Study.tryGetByIdentifier studyIdentifier investigation.Studies.Value with
            | Some study ->
                let assayFileNames = study.Assays.Value |> List.map (fun a -> a.FileName.Value)
                let assayOption = API.Assay.tryGetByFileName newAssayFileName study.Assays.Value
                Expect.isSome assayOption "New Assay was not added to study"
                Expect.equal assayOption.Value testAssay "New Assay is missing values"
                Expect.sequenceEqual assayFileNames [oldAssayFileName;newAssayFileName] "Either an assay is missing or they're in an incorrect order"
            | None ->
                failwith "Study was removed from the investigation"
        )
        testCase "CreateStudyIfNotExisting" (fun () -> 
            let assayIdentifier = "TestAssay"
            let assayFileName = IsaModelConfiguration.getAssayFileName assayIdentifier configuration
            let measurementType = "MeasurementTypeOfStudyCreatedByAssayRegister"
            let studyIdentifier = "StudyCreatedByAssayRegister"
            
            let assayArgs : AssayRegisterArgs list = [AssayRegisterArgs.StudyIdentifier studyIdentifier;AssayRegisterArgs.AssayIdentifier assayIdentifier;MeasurementType "MeasurementTypeOfStudyCreatedByAssayRegister"]
            let testAssay = ISADotNet.XLSX.Assays.fromString measurementType "" "" "" "" "" "" assayFileName []
            
            processCommand configuration AssayAPI.register assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            let studyOption = API.Study.tryGetByIdentifier studyIdentifier investigation.Studies.Value
            Expect.isSome studyOption "Study should have been created in order for assay to be placed in it"     
            
            let assayOption = API.Assay.tryGetByFileName assayFileName studyOption.Value.Assays.Value
            Expect.isSome assayOption "Assay was not added to newly created study"
            Expect.equal assayOption.Value testAssay "Assay is missing values"
        )
        testCase "StudyNameNotGivenUseAssayName" (fun () -> 
            let assayIdentifier = "TestAssayWithoutStudyName"
            let assayFileName = IsaModelConfiguration.getAssayFileName assayIdentifier configuration
            let technologyType = "InferName"
            
            let assayArgs = [AssayRegisterArgs.AssayIdentifier assayIdentifier;TechnologyType technologyType]
            let testAssay = ISADotNet.XLSX.Assays.fromString "" "" "" technologyType "" "" "" assayFileName []
            
            processCommand configuration AssayAPI.register assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            let studyOption = API.Study.tryGetByIdentifier assayIdentifier investigation.Studies.Value
            Expect.isSome studyOption "Study should have been created with the name of the assay but was not"     
            
            let assayOption = API.Assay.tryGetByFileName assayFileName studyOption.Value.Assays.Value
            Expect.isSome assayOption "Assay was not added to newly created study"
            Expect.equal assayOption.Value testAssay "Assay is missing values"
        )
        testCase "StudyNameNotGivenUseAssayNameNoDuplicateStudy" (fun () -> 
            
            let assayIdentifier = "StudyCreatedByAssayRegister"
            let assayFileName = IsaModelConfiguration.getAssayFileName assayIdentifier configuration

            let assayArgs = [AssayRegisterArgs.AssayIdentifier assayIdentifier]
            let testAssay = ISADotNet.XLSX.Assays.fromString "" "" "" "" "" "" "" assayFileName []
            
            processCommand configuration AssayAPI.register assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            
            let studiesWithIdentifiers = investigation.Studies.Value |> List.filter (fun s -> s.Identifier.Value = assayIdentifier)
            
            Expect.equal studiesWithIdentifiers.Length 1 "Duplicate study was added"

            let study = API.Study.tryGetByIdentifier assayIdentifier investigation.Studies.Value |> Option.get

            let assayOption = API.Assay.tryGetByFileName assayFileName study.Assays.Value
            Expect.isSome assayOption "New Assay was not added to study"
            Expect.equal assayOption.Value testAssay "New Assay is missing values"
            Expect.equal study.Assays.Value.Length 2 "Assay missing"
        )
        testCase "StudyNameNotGivenUseAssayNameNoDuplicateAssay" (fun () ->             
            let assayIdentifier = "StudyCreatedByAssayRegister"
            let assayFileName = IsaModelConfiguration.getAssayFileName assayIdentifier configuration

            let assayArgs = [AssayRegisterArgs.AssayIdentifier assayIdentifier]            

            processCommand configuration AssayAPI.register assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            
            let studiesWithIdentifiers = investigation.Studies.Value |> List.filter (fun s -> s.Identifier.Value = assayIdentifier)
            
            Expect.equal studiesWithIdentifiers.Length 1 "Duplicate study was added"

            let study = API.Study.tryGetByIdentifier assayIdentifier investigation.Studies.Value |> Option.get

            let assayOption = API.Assay.tryGetByFileName assayFileName study.Assays.Value
            Expect.isSome assayOption "Assay was removed"
            Expect.equal study.Assays.Value.Length 2 "Duplicate Assay added"
        )
    ]
    |> testSequenced

[<Tests>]
let testAssayUpdate = 

    let testDirectory = __SOURCE_DIRECTORY__ + @"/TestResult/assayUpdateTest"

    let configuration = 
        ArcConfiguration.create 
            (Map.ofList ["workdir",testDirectory;"verbosity","2"]) 
            (standardISAArgs)
            Map.empty Map.empty Map.empty Map.empty

    testList "AssayUpdateTests" [

        testCase "UpdateStandard" (fun () -> 

            let assay1Args = [AssayRegisterArgs.StudyIdentifier "Study1"; AssayRegisterArgs.AssayIdentifier "Assay1";AssayRegisterArgs.MeasurementType "Assay1Method"]
            let assay2Args = [AssayRegisterArgs.StudyIdentifier "Study1"; AssayRegisterArgs.AssayIdentifier "Assay2";AssayRegisterArgs.MeasurementType "Assay2Method";AssayRegisterArgs.TechnologyType "Assay2Tech"]
            let assay3Args = [AssayRegisterArgs.StudyIdentifier "Study2"; AssayRegisterArgs.AssayIdentifier "Assay3";AssayRegisterArgs.TechnologyType "Assay3Tech"]

            let studyIdentifier = "Study1"
            let assayIdentifier = "Assay2"
            let assayFileName = IsaModelConfiguration.getAssayFileName assayIdentifier configuration
            let measurementType = "NewMeasurementType"
            let testAssay = ISADotNet.XLSX.Assays.fromString measurementType "" "" "Assay2Tech" "" "" "" assayFileName []

            let assayArgs : AssayUpdateArgs list = [AssayUpdateArgs.StudyIdentifier studyIdentifier;AssayUpdateArgs.AssayIdentifier assayIdentifier;AssayUpdateArgs.MeasurementType measurementType]
            
            setupArc configuration
            processCommand configuration AssayAPI.register assay1Args
            processCommand configuration AssayAPI.register assay2Args
            processCommand configuration AssayAPI.register assay3Args

            let investigationBeforeUpdate = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            processCommand configuration AssayAPI.update assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            

            Expect.equal investigation.Studies.Value.[1] investigationBeforeUpdate.Studies.Value.[1] "Only assay in first study was supposed to be updated, but study 2 is also different"
            
            let study = investigation.Studies.Value.[0]
            let studyBeforeUpdate = investigationBeforeUpdate.Studies.Value.[0]

            Expect.equal study.Assays.Value.[0] studyBeforeUpdate.Assays.Value.[0] "Only assay number 1 in first study was supposed to be updated, but first assay is also different"

            let assay = study.Assays.Value.[1]

            Expect.equal assay.FileName testAssay.FileName "Assay Filename has changed even though it shouldnt"
            Expect.equal assay.TechnologyType testAssay.TechnologyType "Assay technology type has changed, even though no value was given and the \"ReplaceWithEmptyValues\" flag was not set"
            Expect.equal assay.MeasurementType testAssay.MeasurementType "Assay Measurement type was not updated correctly"

        )
        testCase "UpdateReplaceWithEmpty" (fun () -> 
           
            let studyIdentifier = "Study2"
            let assayIdentifier = "Assay3"
            let assayFileName = IsaModelConfiguration.getAssayFileName assayIdentifier configuration
            let measurementType = "NewMeasurementType"
            let testAssay = ISADotNet.XLSX.Assays.fromString measurementType "" "" "" "" "" "" assayFileName []

            let assayArgs : AssayUpdateArgs list = [AssayUpdateArgs.ReplaceWithEmptyValues; AssayUpdateArgs.StudyIdentifier studyIdentifier;AssayUpdateArgs.AssayIdentifier assayIdentifier;AssayUpdateArgs.MeasurementType measurementType]           

            let investigationBeforeUpdate = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            processCommand configuration AssayAPI.update assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)            

            Expect.equal investigation.Studies.Value.[0] investigationBeforeUpdate.Studies.Value.[0] "Only assay in second study was supposed to be updated, but study 1 is also different"
            
            let study = investigation.Studies.Value.[1]

            let assay = study.Assays.Value.[0]

            Expect.equal assay.FileName testAssay.FileName "Assay Filename has changed even though it shouldnt"
            Expect.isNone assay.TechnologyType "Assay technology type has not been removed, even though no value was given and the \"ReplaceWithEmptyValues\" flag was set"
            Expect.equal assay.MeasurementType testAssay.MeasurementType "Assay Measurement type was not updated correctly"
        )
    ]
    |> testSequenced

[<Tests>]
let testAssayUnregister = 

    let testDirectory = __SOURCE_DIRECTORY__ + @"/TestResult/assayUnregisterTest"

    let configuration = 
        ArcConfiguration.create 
            (Map.ofList ["workdir",testDirectory;"verbosity","2"]) 
            (standardISAArgs)
            Map.empty Map.empty Map.empty Map.empty

    testList "AssayUnregisterTests" [

        testCase "AssayExists" (fun () -> 

            let assay1Args = [AssayRegisterArgs.StudyIdentifier "Study1"; AssayRegisterArgs.AssayIdentifier "Assay1";AssayRegisterArgs.MeasurementType "Assay1Method"]
            let assay2Args = [AssayRegisterArgs.StudyIdentifier "Study1"; AssayRegisterArgs.AssayIdentifier "Assay2";AssayRegisterArgs.MeasurementType "Assay2Method";AssayRegisterArgs.TechnologyType "Assay2Tech"]
            let assay3Args = [AssayRegisterArgs.StudyIdentifier "Study2"; AssayRegisterArgs.AssayIdentifier "Assay3";AssayRegisterArgs.TechnologyType "Assay3Tech"]

            let studyIdentifier = "Study1"
            let assayIdentifier = "Assay2"

            let assayArgs : AssayUnregisterArgs list = [AssayUnregisterArgs.StudyIdentifier studyIdentifier;AssayUnregisterArgs.AssayIdentifier assayIdentifier]
            
            setupArc configuration
            processCommand configuration AssayAPI.register assay1Args
            processCommand configuration AssayAPI.register assay2Args
            processCommand configuration AssayAPI.register assay3Args

            let investigationBeforeUpdate = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            processCommand configuration AssayAPI.unregister assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            

            Expect.equal investigation.Studies.Value.[1] investigationBeforeUpdate.Studies.Value.[1] "Only assay in first study was supposed to be unregistered, but study 2 is also different"
            
            let study = investigation.Studies.Value.[0]
            let studyBeforeUpdate = investigationBeforeUpdate.Studies.Value.[0]

            Expect.isSome study.Assays "Only assay number 2 in first study was supposed to be unregistered, but both assays were removed"
            Expect.notEqual study.Assays.Value [] "Only assay number 2 in first study was supposed to be unregistered, but both assays were removed"
            Expect.equal study.Assays.Value.[0] studyBeforeUpdate.Assays.Value.[0] "Only assay number 2 in first study was supposed to be unregistered, but first assay is also different"
            Expect.equal study.Assays.Value.Length 1 "Only first assay was supposed to be left in the study after removing the second but both are still present"
        )
        testCase "AssayDoesNotExist" (fun () -> 
           
            let studyIdentifier = "Study2"
            let assayIdentifier = "FakeAssayName"

            let assayArgs : AssayUnregisterArgs list = [AssayUnregisterArgs.StudyIdentifier studyIdentifier;AssayUnregisterArgs.AssayIdentifier assayIdentifier]

            let investigationBeforeUpdate = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            processCommand configuration AssayAPI.unregister assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)            

            Expect.equal investigation investigationBeforeUpdate "Investigation values did change even though the given assay does not exist and none should have been removed"
            
        )
        testCase "StudyDoesNotExist" (fun () -> 
           
            let studyIdentifier = "FakeStudyName"
            let assayIdentifier = "Assay2"

            let assayArgs : AssayUnregisterArgs list = [AssayUnregisterArgs.StudyIdentifier studyIdentifier;AssayUnregisterArgs.AssayIdentifier assayIdentifier]

            let investigationBeforeUpdate = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            processCommand configuration AssayAPI.unregister assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)            

            Expect.equal investigation investigationBeforeUpdate "Investigation values did change even though the given study does not exist and none should have been removed"
            
        )
    ]
    |> testSequenced

[<Tests>]
let testAssayMove = 

    let testDirectory = __SOURCE_DIRECTORY__ + @"/TestResult/assayMoveTest"

    let configuration = 
        ArcConfiguration.create 
            (Map.ofList ["workdir",testDirectory;"verbosity","2"]) 
            (standardISAArgs)
            Map.empty Map.empty Map.empty Map.empty

    testList "AssayMoveTests" [

        testCase "ToExistingStudy" (fun () -> 

            let assay1Args = [AssayRegisterArgs.StudyIdentifier "Study1"; AssayRegisterArgs.AssayIdentifier "Assay1";AssayRegisterArgs.MeasurementType "Assay1Method"]
            let assay2Args = [AssayRegisterArgs.StudyIdentifier "Study1"; AssayRegisterArgs.AssayIdentifier "Assay2";AssayRegisterArgs.MeasurementType "Assay2Method";AssayRegisterArgs.TechnologyType "Assay2Tech"]
            let assay3Args = [AssayRegisterArgs.StudyIdentifier "Study2"; AssayRegisterArgs.AssayIdentifier "Assay3";AssayRegisterArgs.TechnologyType "Assay3Tech"]

            let studyIdentifier = "Study1"
            let targetStudyIdentfier = "Study2"
            let assayIdentifier = "Assay2"

            let assayArgs : AssayMoveArgs list = [AssayMoveArgs.StudyIdentifier studyIdentifier;AssayMoveArgs.TargetStudyIdentifier targetStudyIdentfier;AssayMoveArgs.AssayIdentifier assayIdentifier]
            
            setupArc configuration
            processCommand configuration AssayAPI.register assay1Args
            processCommand configuration AssayAPI.register assay2Args
            processCommand configuration AssayAPI.register assay3Args

            let investigationBeforeUpdate = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            let testAssay = investigationBeforeUpdate.Studies.Value.[0].Assays.Value.[1]
            let fileName = testAssay.FileName.Value

            processCommand configuration AssayAPI.move assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            
            let studies = investigation.Studies.Value

            Expect.isNone (API.Assay.tryGetByFileName fileName studies.[0].Assays.Value) "Assay was not removed from source study"

            let assay = API.Assay.tryGetByFileName fileName studies.[1].Assays.Value

            Expect.isSome assay "Assay was not added to target study"

            Expect.equal assay.Value testAssay "Assay was moved but some values are not correct"
            
        )
        testCase "ToNewStudy" (fun () -> 

            let studyIdentifier = "Study1"
            let targetStudyIdentfier = "NewStudy"
            let assayIdentifier = "Assay1"

            let assayArgs : AssayMoveArgs list = [AssayMoveArgs.StudyIdentifier studyIdentifier;AssayMoveArgs.TargetStudyIdentifier targetStudyIdentfier;AssayMoveArgs.AssayIdentifier assayIdentifier]
 
            let investigationBeforeUpdate = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            let testAssay = investigationBeforeUpdate.Studies.Value.[0].Assays.Value.[0]
            let fileName = testAssay.FileName.Value

            processCommand configuration AssayAPI.move assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            
            let studies = investigation.Studies.Value

            Expect.isNone (studies.[0].Assays |> Option.defaultValue [] |> API.Assay.tryGetByFileName fileName) "Assay was not removed from source study"

            let study = API.Study.tryGetByIdentifier "NewStudy" studies

            Expect.isSome study "New Study was not created"

            let assay = API.Assay.tryGetByFileName fileName study.Value.Assays.Value

            Expect.isSome assay "Assay was not added to target study"

            Expect.equal assay.Value testAssay "Assay was moved but some values are not correct"
            
        )
        testCase "AssayDoesNotExist" (fun () -> 
           
            let studyIdentifier = "Study2"
            let targetStudyIdentifier = "Study1"
            let assayIdentifier = "FakeAssayName"

            let assayArgs : AssayMoveArgs list = [AssayMoveArgs.StudyIdentifier studyIdentifier;AssayMoveArgs.TargetStudyIdentifier targetStudyIdentifier;AssayMoveArgs.AssayIdentifier assayIdentifier]

            let investigationBeforeUpdate = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            processCommand configuration AssayAPI.move assayArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)            

            Expect.equal investigation investigationBeforeUpdate "Investigation values did change even though the given assay does not exist and none should have been moved"
            
        )
    ]
    |> testSequenced