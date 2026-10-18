import Link from "next/link";

export default function NotFound() {
  return (
    <div className="text-sm text-muted">
      <p>This page does not exist.</p>
      <Link href="/memories" className="mt-2 inline-block text-accent hover:underline">
        Back to memories
      </Link>
    </div>
  );
}
