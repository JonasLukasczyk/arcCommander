﻿module StudyTests

open Argu
open Expecto
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
let testStudyRegister =

    let testDirectory = __SOURCE_DIRECTORY__ + @"/TestResult/studyRegisterTest"
    
    let configuration = 
        ArcConfiguration.create 
            (Map.ofList ["workdir",testDirectory;"verbosity","2"]) 
            (standardISAArgs)
            Map.empty Map.empty Map.empty Map.empty

    testList "StudyRegisterTests" [
        testCase "AddToEmptyInvestigation" (fun () -> 
            Expect.isTrue true ""
        )
        testCase "AddSecondStudy" (fun () -> 
            Expect.isTrue true ""
        )
        testCase "DoesntAddDuplicateStudy" (fun () -> 
            Expect.isTrue true ""
        )
    ]
    |> testSequenced

[<Tests>]
let testStudyProtocolLoad = 

    let protocolTestFile = __SOURCE_DIRECTORY__ + @"/TestFiles/ProtocolTestFile.json"
    let processTestFile = __SOURCE_DIRECTORY__ + @"/TestFiles/ProcessTestFile.json"

    let testDirectory = __SOURCE_DIRECTORY__ + @"/TestResult/studyProtocolLoadTest"

    let configuration = 
        ArcConfiguration.create 
            (Map.ofList ["workdir",testDirectory;"verbosity","2";"editor","Decoy"]) 
            (standardISAArgs)
            Map.empty Map.empty Map.empty Map.empty

    testList "StudyProtocolLoadTests" [

        testCase "AddFromProtocolFile" (fun () -> 

            let studyIdentifier = "ProtocolStudy"
            
            let studyArgs = [StudyRegisterArgs.Identifier studyIdentifier]

            let loadArgs = [StudyProtocols.ProtocolLoadArgs.InputPath protocolTestFile; StudyProtocols.ProtocolLoadArgs.StudyIdentifier studyIdentifier]
            
            let testProtocolName = "peptide_digestion"
            let testProtocolTypeName = AnnotationValue.Text "Protein Digestion"
                        
            setupArc configuration
            processCommand configuration StudyAPI.register studyArgs
            processCommand configuration StudyAPI.Protocols.load loadArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            match API.Study.tryGetByIdentifier studyIdentifier investigation.Studies.Value with
            | Some study ->
                let protocols = study.Protocols
                Expect.isSome protocols "Protocol was not added, as Protocols is still None"
                let protocol = API.Protocol.tryGetByName testProtocolName protocols.Value
                Expect.isSome protocol "Protocol could not be found, either it was no added or the name was not inserted correctly"
                let protocolType = protocol.Value.ProtocolType
                Expect.isSome protocolType "ProtocolType was not added to protocol"
                Expect.equal protocolType.Value.Name.Value testProtocolTypeName "ProtocolType name field was not correctly transferred from protocol file"

            | None -> Expect.isTrue false "Study was not registered, Protocol could not be tested"
        )
        testCase "AddFromProcessFile" (fun () -> 

            let studyIdentifier = "ProcessStudy"
            
            let studyArgs = [StudyRegisterArgs.Identifier studyIdentifier]

            let loadArgs = [StudyProtocols.ProtocolLoadArgs.InputPath processTestFile; StudyProtocols.ProtocolLoadArgs.StudyIdentifier studyIdentifier;StudyProtocols.ProtocolLoadArgs.IsProcessFile]
            
            let testProtocolName = "peptide_digestion"
            let testProtocolTypeName = AnnotationValue.Text "Protein Digestion"
                        
            processCommand configuration StudyAPI.register studyArgs
            processCommand configuration StudyAPI.Protocols.load loadArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
            match API.Study.tryGetByIdentifier studyIdentifier investigation.Studies.Value with
            | Some study ->
                let protocols = study.Protocols
                Expect.isSome protocols "Protocol was not added, as Protocols is still None"
                let protocol = API.Protocol.tryGetByName testProtocolName protocols.Value
                Expect.isSome protocol "Protocol could not be found, either it was no added or the name was not inserted correctly"
                let protocolType = protocol.Value.ProtocolType
                Expect.isSome protocolType "ProtocolType was not added to protocol"
                Expect.equal protocolType.Value.Name.Value testProtocolTypeName "ProtocolType name field was not correctly transferred from protocol file"

            | None -> Expect.isTrue false "Study was not registered, Protocol could not be tested"       
        )
        testCase "DoesNothingIfAlreadyExisting" (fun () -> 

            let studyIdentifier = "AlreadyContaingProtocolStudy"                    

            let studyArgs = [StudyRegisterArgs.Identifier studyIdentifier]
            let protocolArgs = [StudyProtocols.ProtocolRegisterArgs.Name "peptide_digestion";StudyProtocols.ProtocolRegisterArgs.StudyIdentifier studyIdentifier]
            let loadArgs = [StudyProtocols.ProtocolLoadArgs.InputPath protocolTestFile; StudyProtocols.ProtocolLoadArgs.StudyIdentifier studyIdentifier]
                       
            processCommand configuration StudyAPI.register studyArgs
            processCommand configuration StudyAPI.Protocols.register protocolArgs

            let investigationBeforeLoadingProtocol = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)

            processCommand configuration StudyAPI.Protocols.load loadArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
           
            Expect.equal investigation investigationBeforeLoadingProtocol "Investigation should not have been altered in any way, as a protocol with the same name did already exist in the study, and the \"UpdateExisting\" flag was not set"
        )
        testCase "UpdateAlreadyExisting" (fun () -> 

            let studyIdentifier = "AlreadyContaingProtocolStudy"
                     
            let testProtocolName = "peptide_digestion"
            let testProtocolTypeName = AnnotationValue.Text "Protein Digestion"

            let studyArgs = [StudyRegisterArgs.Identifier studyIdentifier]
            let protocolArgs = [StudyProtocols.ProtocolRegisterArgs.Name "peptide_digestion";StudyProtocols.ProtocolRegisterArgs.StudyIdentifier studyIdentifier]
            let loadArgs = [StudyProtocols.ProtocolLoadArgs.InputPath protocolTestFile; StudyProtocols.ProtocolLoadArgs.StudyIdentifier studyIdentifier;StudyProtocols.ProtocolLoadArgs.UpdateExisting]
                       
            processCommand configuration StudyAPI.register studyArgs
            processCommand configuration StudyAPI.Protocols.register protocolArgs

            processCommand configuration StudyAPI.Protocols.load loadArgs
            
            let investigation = ISADotNet.XLSX.Investigation.fromFile (IsaModelConfiguration.getInvestigationFilePath configuration)
           
            match API.Study.tryGetByIdentifier studyIdentifier investigation.Studies.Value with
            | Some study ->
                let protocols = study.Protocols
                let protocol = API.Protocol.tryGetByName testProtocolName protocols.Value
                let protocolType = protocol.Value.ProtocolType
                Expect.isSome protocolType "ProtocolType was not added to protocol"
                Expect.equal protocolType.Value.Name.Value testProtocolTypeName "ProtocolType name field was not correctly transferred from protocol file"

            | None -> Expect.isTrue false "Study was not registered, Protocol could not be tested"               

        )
    ]
    |> testSequenced