export interface FinishedListener{
    finishedListener(error: Error | null, stdout: string, stderr: string): void
}
export interface CurrentWorkingDirectory{
    cwd?:string
}
export interface CommandArguments{
    args:string[]
}
export interface CommandInputList{
    inputs:string[]
}
export interface CommandOutput{
    output:string
}
export interface CommandInput{
    input:string
}
export interface CommandFilename{
    filename:string
}
export interface CommandFormat{
    format:string
}
export interface CommandURL{
    url:string
}
