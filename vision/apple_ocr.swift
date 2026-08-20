import Foundation
import ImageIO
import Vision

let data = FileHandle.standardInput.readDataToEndOfFile()
guard let source = CGImageSourceCreateWithData(data as CFData, nil),
      let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
    exit(2)
}

let request = VNRecognizeTextRequest()
request.recognitionLevel = .accurate
request.usesLanguageCorrection = false
request.recognitionLanguages = ["en-US"]
request.minimumTextHeight = 0.08

try VNImageRequestHandler(cgImage: image).perform([request])
for observation in request.results ?? [] {
    for candidate in observation.topCandidates(3) {
        print("\(candidate.confidence)\t\(candidate.string)")
    }
}
