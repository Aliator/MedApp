/**
 * A specimen label's QR code, from the server's SVG path (the dark modules) in a square `size` modules wide, quiet zone
 * included. It holds the specimen's number only, never patient details (decision 87): a keyboard scanner reads it
 * into the Lab page's Find. QR codes must be dark on light for scanners and cameras, whatever the app's colours, so
 * like two-factor's QR code this is one of the few places with fixed colours (black on white).
 */
export function SpecimenQrCode({path, size, number, displaySize = 96}: {path: string; size: number; number: string; displaySize?: number}) {
  if (!path || size <= 0) return null;
  return (
    <svg
      role="img"
      aria-label={`QR code of sample number ${number}`}
      viewBox={`0 0 ${size} ${size}`}
      width={displaySize}
      height={displaySize}
      shapeRendering="crispEdges">
      <rect width={size} height={size} fill="#ffffff" />
      <path d={path} fill="#000000" />
    </svg>
  );
}
