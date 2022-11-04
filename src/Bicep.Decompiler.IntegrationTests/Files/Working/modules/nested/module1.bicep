param _artifactsLocation string = deployment().properties.templateLink.uri
//@[6:14) [no-unused-params (Warning)] Parameter "_artifactsLocation" is declared but never used. (CodeDescription: bicep core(https://aka.ms/bicep/linter/no-unused-params)) |location|
@secure()
param _artifactsLocationSasToken string = ''
//@[6:14) [no-unused-params (Warning)] Parameter "_artifactsLocationSasToken" is declared but never used. (CodeDescription: bicep core(https://aka.ms/bicep/linter/no-unused-params)) |location|
param location string
//@[6:14) [no-unused-params (Warning)] Parameter "location" is declared but never used. (CodeDescription: bicep core(https://aka.ms/bicep/linter/no-unused-params)) |location|
param objectParam object
//@[6:17) [no-unused-params (Warning)] Parameter "objectParam" is declared but never used. (CodeDescription: bicep core(https://aka.ms/bicep/linter/no-unused-params)) |objectParam|
param arrayParam array
//@[6:16) [no-unused-params (Warning)] Parameter "arrayParam" is declared but never used. (CodeDescription: bicep core(https://aka.ms/bicep/linter/no-unused-params)) |arrayParam|
param optionalParam string = 'hello!'

output sampleOutput string = optionalParam
